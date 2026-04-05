using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinSerialMonitor
{
    public partial class Form1 : Form
    {
        private bool isConnected = false;
        private StringBuilder receivedData = new StringBuilder();

        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
        }

        private void InitializeCustomComponents()
        {
            // Заполнение comboBox2 скоростями
            comboBox2.Items.AddRange(new object[] {
                "300", "1200", "2400", "4800", "9600", "19200",
                "38400", "57600", "115200", "230400", "460800", "921600"
            });
            comboBox2.SelectedItem = "9600"; // Скорость по умолчанию

            // Настройка serialPort
            serialPort1.DataReceived += SerialPort1_DataReceived;
            serialPort1.NewLine = "\n"; // Устанавливаем символ новой строки
            serialPort1.Encoding = Encoding.UTF8; // Явно указываем кодировку
            serialPort1.ReadTimeout = 1000;
            serialPort1.WriteTimeout = 1000;
            serialPort1.DtrEnable = true; // Включаем DTR для Arduino
            serialPort1.RtsEnable = true; // Включаем RTS для Arduino

            // Обновление списка портов при запуске
            UpdatePortList();

            // Установка начального состояния UI (неактивное)
            SetUIConnectedState(false);
        }

        private void UpdatePortList()
        {
            try
            {
                string[] ports = SerialPort.GetPortNames();
                comboBox1.Items.Clear();
                comboBox1.Items.AddRange(ports);
                if (comboBox1.Items.Count > 0)
                    comboBox1.SelectedIndex = 0;
                else
                    comboBox1.Text = "Нет доступных портов";
            }
            catch (Exception ex)
            {
                AddMessageToLog($"Ошибка при обновлении портов: {ex.Message}", true);
            }
        }

        private StringBuilder receiveBuffer = new StringBuilder();

        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // Читаем все доступные данные
                string data = serialPort1.ReadExisting();

                if (!string.IsNullOrEmpty(data))
                {
                    lock (receiveBuffer)
                    {
                        receiveBuffer.Append(data);

                        // Проверяем, есть ли полные строки (с разделителем \n)
                        string bufferContent = receiveBuffer.ToString();
                        int newlineIndex;

                        while ((newlineIndex = bufferContent.IndexOf('\n')) >= 0)
                        {
                            // Извлекаем полную строку
                            string completeLine = bufferContent.Substring(0, newlineIndex);
                            receiveBuffer.Remove(0, newlineIndex + 1);

                            // Убираем символ \r если есть
                            completeLine = completeLine.TrimEnd('\r');

                            if (!string.IsNullOrEmpty(completeLine))
                            {
                                string line = completeLine;
                                this.Invoke(new Action(() =>
                                {
                                    AddMessageToLog(line, false);
                                }));
                            }

                            bufferContent = receiveBuffer.ToString();
                        }
                    }
                }
            }
            catch (TimeoutException)
            {
                // Игнорируем таймаут
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    AddMessageToLog($"Ошибка при получении данных: {ex.Message}", true);
                }));
            }
        }

        private void AddMessageToLog(string message, bool isError)
        {
            // Получаем состояние чек-бокса (показывать время или нет)
            bool showTimestamp = checkBox1.Checked;

            // Разделяем сообщение на строки для лучшего форматирования
            string[] lines = message.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    string formattedMessage;

                    if (showTimestamp)
                    {
                        // С отметкой времени
                        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                        formattedMessage = $"[{timestamp}] {line}";

                        if (isError)
                            formattedMessage = $"[{timestamp}] [ОШИБКА] {line}";
                    }
                    else
                    {
                        // Без отметки времени
                        formattedMessage = line;

                        if (isError)
                            formattedMessage = $"[ОШИБКА] {line}";
                    }

                    textBox1.AppendText(formattedMessage + Environment.NewLine);
                }
            }

            // Прокрутка в конец
            textBox1.ScrollToCaret();
        }

        private void SetUIConnectedState(bool connected)
        {
            // Кнопки
            button3.Enabled = connected;  // Send
            button4.Enabled = connected;  // Clear
            button5.Enabled = connected;  // Save

            // Текстовые поля
            textBox2.Enabled = connected; // Поле ввода сообщений
            textBox2.BackColor = connected ? SystemColors.Window : SystemColors.Control;

            textBox1.Enabled = connected; // Поле ввода сообщений
            textBox1.BackColor = connected ? SystemColors.Window : SystemColors.Control;

            // Чек-бокс (можно оставить активным всегда для изменения формата лога)
            checkBox1.Enabled = connected;

            // Визуальный индикатор
            if (connected)
            {
            }
            else
            {
                textBox2.Text = "";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            UpdatePortList();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                if (!isConnected)
                {
                    // Проверка выбран ли порт
                    if (comboBox1.SelectedItem == null || comboBox1.Text == "Нет доступных портов")
                    {
                        MessageBox.Show("Пожалуйста, выберите доступный порт!", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Настройка параметров порта
                    serialPort1.PortName = comboBox1.SelectedItem.ToString();
                    serialPort1.BaudRate = int.Parse(comboBox2.SelectedItem.ToString());
                    serialPort1.DataBits = 8;
                    serialPort1.StopBits = StopBits.One;
                    serialPort1.Parity = Parity.None;
                    serialPort1.Handshake = Handshake.None;

                    // Настройки для Arduino
                    serialPort1.DtrEnable = true;  // Сброс Arduino при подключении
                    serialPort1.RtsEnable = true;

                    // Открытие порта
                    serialPort1.Open();

                    // Даем время Arduino на перезагрузку (если DTR включен)
                    if (serialPort1.DtrEnable)
                        System.Threading.Thread.Sleep(2000);

                    isConnected = true;

                    button2.Text = "Disconnect";
                    button2.BackColor = Color.LightCoral;
                    comboBox1.Enabled = false;
                    comboBox2.Enabled = false;
                    button1.Enabled = false;

                    // Активация UI элементов
                    SetUIConnectedState(true);

                    AddMessageToLog($"Подключено к {serialPort1.PortName} на скорости {serialPort1.BaudRate} бод", false);
                }
                else
                {
                    // Закрытие порта
                    if (serialPort1.IsOpen)
                    {
                        serialPort1.Close();
                    }
                    isConnected = false;

                    button2.Text = "Connect";
                    button2.BackColor = SystemColors.Control;
                    comboBox1.Enabled = true;
                    comboBox2.Enabled = true;
                    button1.Enabled = true;

                    // Деактивация UI элементов
                    SetUIConnectedState(false);

                    AddMessageToLog($"Отключено от {serialPort1.PortName}", false);
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Нет доступа к порту. Возможно, порт уже используется другим приложением.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при подключении: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddMessageToLog($"Ошибка подключения: {ex.Message}", true);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("Сначала подключитесь к устройству!", "Предупреждение",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string messageToSend = textBox2.Text.Trim();

            if (string.IsNullOrEmpty(messageToSend))
            {
                MessageBox.Show("Введите сообщение для отправки!", "Предупреждение",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Отправка сообщения с символом новой строки
                serialPort1.Write(messageToSend + "\n");

                // Добавление отправленного сообщения в лог
                AddMessageToLog($"Отправлено: {messageToSend}", false);

                // Очистка поля ввода
                textBox2.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddMessageToLog($"Ошибка отправки: {ex.Message}", true);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            textBox1.Clear();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox1.Text))
            {
                MessageBox.Show("Нет данных для сохранения!", "Предупреждение",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.DefaultExt = "txt";
            saveFileDialog.FileName = $"serial_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, textBox1.Text, Encoding.UTF8);
                    MessageBox.Show($"Данные успешно сохранены в файл:\n{saveFileDialog.FileName}",
                        "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Отправка сообщения по нажатию Enter (только если соединение активно)
            if (e.KeyChar == (char)Keys.Enter && isConnected)
            {
                e.Handled = true;
                button3_Click(sender, e);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Закрытие порта при закрытии приложения
            if (isConnected && serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.Close();
                }
                catch (Exception ex)
                {
                    // Игнорируем ошибки при закрытии
                    System.Diagnostics.Debug.WriteLine($"Error closing port: {ex.Message}");
                }
            }
            base.OnFormClosing(e);
        }
    }
}