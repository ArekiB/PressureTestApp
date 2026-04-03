using System;
using System.Threading.Tasks;
using System.Net.Sockets;
using Modbus.Device;

namespace PressureTestApp.Services
{
    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Timeout,
        Error
    }

    public class ModbusService
    {
        private TcpClient _tcpClient;
        private ModbusIpMaster _master;
        private string _ipAddress;
        private int _port;
        private int _slaveId;
        private bool _isConnected;

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
        public event EventHandler<ConnectionStatus> StatusChanged;

        public ModbusService(string ipAddress, int port, int slaveId)
        {
            _ipAddress = ipAddress;
            _port = port;
            _slaveId = slaveId;
        }

        public void UpdateSettings(string ipAddress, int port, int slaveId)
        {
            _ipAddress = ipAddress;
            _port = port;
            _slaveId = slaveId;

            if (_isConnected)
            {
                Disconnect();
                _ = ConnectAsync();
            }
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                Status = ConnectionStatus.Connecting;
                StatusChanged?.Invoke(this, Status);

                // Отладка через событие
                System.Diagnostics.Debug.WriteLine($"Modbus: попытка подключения к {_ipAddress}:{_port}");

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_ipAddress, _port);

                System.Diagnostics.Debug.WriteLine($"Modbus: TCP подключён");

                _master = ModbusIpMaster.CreateIp(_tcpClient);
                _master.Transport.ReadTimeout = 1000;
                _master.Transport.WriteTimeout = 1000;

                _isConnected = true;
                Status = ConnectionStatus.Connected;
                StatusChanged?.Invoke(this, Status);

                System.Diagnostics.Debug.WriteLine($"Modbus: мастер создан, подключение успешно");

                return true;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Modbus: Таймаут - {ex.Message}");
                Status = ConnectionStatus.Timeout;
                StatusChanged?.Invoke(this, Status);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Modbus: Ошибка - {ex.Message}");
                Status = ConnectionStatus.Error;
                StatusChanged?.Invoke(this, Status);
                return false;
            }
        }

        public void Disconnect()
        {
            _master?.Dispose();
            _tcpClient?.Close();
            _isConnected = false;
            Status = ConnectionStatus.Disconnected;
            StatusChanged?.Invoke(this, Status);
        }

        /// <summary>
        /// Преобразует "удобный" Modbus адрес (например, 40401) в "чистый" адрес смещения (например, 400).
        /// </summary>
        /// <param name="plcAddress">Адрес в формате PLC (например, 40401).</param>
        /// <returns>Чистый адрес для использования в библиотеке NModbus4.</returns>
        public static int ConvertPlcAddressToModbus(int plcAddress)
        {
            // Если адрес меньше 40001, возможно, это уже чистый адрес.
            if (plcAddress < 40001)
            {
                return plcAddress;
            }
            // Преобразуем адрес, вычитая базовое значение для Holding Registers (40001)
            // и затем вычитаем единицу для перехода к нулевой базе.
            return plcAddress - 40001;
        }

        public async Task<double> ReadValueAsync(int address, string dataType, double scale = 1.0)
        {
            if (!_isConnected || _master == null)
            {
                System.Diagnostics.Debug.WriteLine($"ReadValueAsync: не подключен");
                return 0;
            }

            // Конвертируем адрес
            int originalAddress = address;
            address = ConvertPlcAddressToModbus(address);
            System.Diagnostics.Debug.WriteLine($"ConvertPlcAddressToModbus: {originalAddress} -> {address}");

            try
            {
                int registersToRead = dataType == "float" ? 2 : 1;
                System.Diagnostics.Debug.WriteLine($"Чтение: адрес={address}, регистров={registersToRead}, тип={dataType}, scale={scale}");

                ushort[] rawData = await _master.ReadHoldingRegistersAsync((byte)_slaveId, (ushort)address, (ushort)registersToRead);

                System.Diagnostics.Debug.WriteLine($"Raw данные: [{string.Join(", ", rawData)}]");

                double value = 0;
                switch (dataType)
                {
                    case "bool":
                        value = rawData[0] > 0 ? 1 : 0;
                        break;
                    case "int":
                        value = (short)rawData[0];
                        break;
                    case "float":
                        value = ConvertFloat(rawData);
                        break;
                    default:
                        value = rawData[0];
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"Результат: {value} -> после scale: {value * scale}");

                return value * scale;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка чтения: {ex.Message}");
                return 0;
            }
        }

        private float ConvertFloat(ushort[] registers)
        {
            if (registers.Length < 2) return 0;

            // Вариант 1: Big Endian (стандарт Modbus, почему то не работает)
            // byte[] bytes = new byte[4];
            // bytes[0] = (byte)(registers[0] >> 8);
            // bytes[1] = (byte)(registers[0] & 0xFF);
            // bytes[2] = (byte)(registers[1] >> 8);
            // bytes[3] = (byte)(registers[1] & 0xFF);

            // Вариант 2: Little Endian (Тест)
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(registers[0] & 0xFF);
            bytes[1] = (byte)(registers[0] >> 8);
            bytes[2] = (byte)(registers[1] & 0xFF);
            bytes[3] = (byte)(registers[1] >> 8);

            return BitConverter.ToSingle(bytes, 0);
        }

        public bool IsConnected => _isConnected;

        // Методы для тестирования
        public async Task<ushort[]> ReadRawRegistersAsync(int address, int count)
        {
            if (!_isConnected || _master == null)
            {
                System.Diagnostics.Debug.WriteLine($"ReadRawRegistersAsync: не подключен");
                return new ushort[count];
            }

            // Конвертируем адрес
            address = ConvertPlcAddressToModbus(address);
            System.Diagnostics.Debug.WriteLine($"ReadRawRegistersAsync: адрес={address}, кол-во={count}");

            try
            {
                ushort[] result = await _master.ReadHoldingRegistersAsync((byte)_slaveId, (ushort)address, (ushort)count);
                System.Diagnostics.Debug.WriteLine($"Raw результат: [{string.Join(", ", result)}]");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
                return new ushort[count];
            }
        }

        public float ConvertToFloat(ushort[] registers)
        {
            return ConvertFloat(registers);
        }
    }
}