using System.Globalization;
using System.Text.RegularExpressions;

namespace SignalAnalysis
{
    class Measurement
    {
        public DateTime DateTime { get; set; }
        public double Signal { get; set; }
    }

    class Program
    {
        static double CalculateStdDev(List<double> values)
        {
            if (values.Count < 2) return 0; // Unbiased estimate requires at least two values
            double mean = values.Average();
            double sumSquares = values.Select(x => (x - mean) * (x - mean)).Sum();
            return Math.Sqrt(sumSquares / (values.Count - 1)); // Use N-1 for unbiased estimate
        }

        static void SaveResultsToFile(string fileName, List<string> results)
        {
            string resultsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results");
            if (!Directory.Exists(resultsDirectory))
            {
                Directory.CreateDirectory(resultsDirectory);
            }

            string dateTimeNow = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string resultFilePath = Path.Combine(resultsDirectory, $"{fileName}_{dateTimeNow}_Расчет.txt");
            File.WriteAllLines(resultFilePath, results);
            Console.WriteLine($"Результаты сохранены в файл: {resultFilePath}");
        }

        static List<string> GetTxtFilesInSignalFolder()
        {
            string signalFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Signal files");
            if (Directory.Exists(signalFolder))
            {
                return Directory.GetFiles(signalFolder, "*.txt").ToList();
            }
            return new List<string>();
        }

        static string GetSavedFilePath(string key)
        {
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
            if (File.Exists(configFilePath))
            {
                var lines = File.ReadAllLines(configFilePath);
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2 && parts[0] == key)
                    {
                        return parts[1];
                    }
                }
            }
            return null;
        }

        static void SaveFilePath(string key, string filePath)
        {
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
            var lines = new List<string>();
            if (File.Exists(configFilePath))
            {
                lines = File.ReadAllLines(configFilePath).ToList();
            }
            lines.RemoveAll(line => line.StartsWith(key + "="));
            lines.Add($"{key}={filePath}");
            File.WriteAllLines(configFilePath, lines);
        }

        static void ProcessSecondFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath).Skip(3).ToList(); // Skip first three lines

            Console.Write("Введите дату измерений (дд.мм.гггг): ");
            string inputDate = Console.ReadLine();
            if (!DateTime.TryParseExact(inputDate, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime filterDate))
            {
                Console.WriteLine("Некорректная дата.");
                return;
            }

            string pattern = @"^(?<index>\d+)\s+(?<name>.+?)\s+-\s+-\s+(?<value>[-+]?\d+,\d+)\s+(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<time>\d{2}:\d{2}:\d{2})";
            Regex regex = new Regex(pattern);

            List<double> values = new List<double>();
            List<DateTime> timestamps = new List<DateTime>();

            lines.Reverse();

            foreach (var line in lines)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    string dateStr = match.Groups["date"].Value;
                    string timeStr = match.Groups["time"].Value;
                    string valueStr = match.Groups["value"].Value.Replace(',', '.');

                    if (DateTime.TryParseExact(dateStr + " " + timeStr, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)
                        && double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double value)
                        && dt.Date == filterDate.Date)
                    {
                        values.Add(value);
                        timestamps.Add(dt);
                    }
                }
            }

            if (values.Count < 5)
            {
                Console.WriteLine("Недостаточно данных для расчёта.");
                return;
            }

            List<string> results = new List<string>();
            int countAbovePoint2 = 0;
            int totalCount = 0;

            for (int i = 0; i <= values.Count - 5; i += 5)
            {
                var window = values.Skip(i).Take(5).ToList();
                double stdDev = CalculateStdDev(window) * 3;

                DateTime startTime = timestamps[i];
                DateTime endTime = timestamps[i + 4];
                string timeInterval = $"(с {startTime:HH:mm:ss} по {endTime:HH:mm:ss})";

                if (stdDev > 0.2)
                {
                    countAbovePoint2++;
                    Console.ForegroundColor = ConsoleColor.Red;
                }

                string result = $"Предел обнаружения: {stdDev:F3} {timeInterval}";
                Console.WriteLine(result);
                results.Add(result);
                Console.ResetColor();
                totalCount++;
            }

            double percentageAbovePoint2 = (double)countAbovePoint2 / totalCount * 100;
            string percentageResult = $"Процент превышений Предела обнаружения выше 0.2: {percentageAbovePoint2:F3}%";
            Console.WriteLine(percentageResult);
            results.Add(percentageResult);

            string outputFileName = $"Расчет предела обнаружения - {filterDate:yyyyMMdd}";
            SaveResultsToFile(outputFileName, results);
        }

        static void CalculateSignalDifference(List<Measurement> measurements, List<string> results)
        {
            int startIndex = measurements.FindIndex(m => (m.DateTime - measurements[0].DateTime).TotalSeconds >= 1800);
            int endIndex = measurements.FindIndex(m => (m.DateTime - measurements[0].DateTime).TotalSeconds >= 3600);

            if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
            {
                Console.WriteLine("Недостаточно данных для автоматического расчета диапазона.");
                return;
            }

            var periodMeasurements = measurements.GetRange(startIndex, endIndex - startIndex + 1);
            double maxSignal = periodMeasurements.Max(m => m.Signal);
            double minSignal = periodMeasurements.Min(m => m.Signal);
            double difference = maxSignal - minSignal;

            Console.Write($"Автоматически рассчитанная разница максимального и минимального значения сигнала (1800-3600 секунд): ");
            if (difference > 30)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else if (difference > 15)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            string differenceResult = $"{difference:F3}";
            Console.WriteLine(differenceResult);
            results.Add($"Автоматически рассчитанная разница максимального и минимального значения сигнала (1800-3600 секунд): {differenceResult}");
            Console.ResetColor();

            string maxResult = $"Максимум: {maxSignal:F3}, Время: {periodMeasurements.First(m => m.Signal == maxSignal).DateTime}, Строка: {measurements.IndexOf(periodMeasurements.First(m => m.Signal == maxSignal)) + 1}";
            string minResult = $"Минимум: {minSignal:F3}, Время: {periodMeasurements.First(m => m.Signal == minSignal).DateTime}, Строка: {measurements.IndexOf(periodMeasurements.First(m => m.Signal == minSignal)) + 1}";
            Console.WriteLine(maxResult);
            Console.WriteLine(minResult);
            results.Add(maxResult);
            results.Add(minResult);
        }

        static void ProcessFirstFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath).ToList();
            if (lines.Count < 2)
            {
                Console.WriteLine("Файл не содержит измерений.");
                return;
            }

            lines.RemoveAt(0);

<<<<<<< HEAD
            
            // Регулярное выражение для извлечения даты, времени и значения сигнала с запятой
=======
>>>>>>> 6f996702159c277e97e3d270ed6450f598931c18
            string pattern = @"^(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<time>\d{2}:\d{2}:\d{2})\s+(?<signal>[-+]?\d+,\d+)";
            Regex regex = new Regex(pattern);

            List<Measurement> measurements = new List<Measurement>();

            foreach (var line in lines)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    string dateStr = match.Groups["date"].Value;
                    string timeStr = match.Groups["time"].Value;
                    string signalStr = match.Groups["signal"].Value.Replace(',', '.');

                    if (DateTime.TryParseExact(dateStr + " " + timeStr, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)
                        && double.TryParse(signalStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double signalVal))
                    {
                        measurements.Add(new Measurement { DateTime = dt, Signal = signalVal });
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка при парсинге сигнала: {signalStr}");
                    }
                }
            }

            if (measurements.Count < 30)
            {
                Console.WriteLine("Недостаточно данных для расчёта по 30 измерениям.");
                return;
            }

            Console.Write("Введите минимальное значение СКО для вывода: ");
            if (!double.TryParse(Console.ReadLine(), out double minStdDev))
            {
                Console.WriteLine("Некорректное значение. Используется значение по умолчанию: 0.7");
                minStdDev = 0.7;
            }

            Console.Write("Введите начальную строку (в секундах) для расчета процента: ");
            if (!double.TryParse(Console.ReadLine(), out double startSeconds))
            {
                Console.WriteLine("Некорректное значение. Используется значение по умолчанию: 1800");
                startSeconds = 1800;
            }

            int countAbovePoint7 = 0;
            int countInRange = 0;
            bool hasStdDevAbovePoint5 = false;

            List<string> results = new List<string>();
            results.Add("Результаты расчёта СКО:");

            List<double> stdDevs = new List<double>();

            for (int i = 29; i < measurements.Count; i++)
            {
                var window = measurements.Skip(i - 29).Take(30).ToList();
                List<double> signals = window.Select(m => m.Signal).ToList();
                double stdDev = CalculateStdDev(signals);

                var lastMeasurement = measurements[i];
                TimeSpan timeSinceStart = lastMeasurement.DateTime - measurements[0].DateTime;

                if (timeSinceStart.TotalSeconds >= startSeconds)
                {
                    stdDevs.Add(stdDev);

                    if (stdDev > minStdDev)
                    {
                        hasStdDevAbovePoint5 = true;

                        if (stdDev > 0.7)
                        {
                            countAbovePoint7++;
                            Console.ForegroundColor = ConsoleColor.Red;
                            string result = $"{lastMeasurement.DateTime.ToString("dd.MM.yyyy HH:mm:ss")} ({timeSinceStart.TotalSeconds:F0} секунд) - СКО: {stdDev:F3}";
                            Console.WriteLine(result);
                            results.Add(result);
                            Console.ResetColor();
                        }
                        else
                        {
                            countInRange++;
                            string result = $"{lastMeasurement.DateTime.ToString("dd.MM.yyyy HH:mm:ss")} ({timeSinceStart.TotalSeconds:F0} секунд) - СКО: {stdDev:F3}";
                            Console.WriteLine(result);
                            results.Add(result);
                        }
                    }
                }
            }

            if (!hasStdDevAbovePoint5)
            {
                string noStdDevAbovePoint5Message = $"Нет значений СКО выше {minStdDev}";
                Console.WriteLine(noStdDevAbovePoint5Message);
                results.Add(noStdDevAbovePoint5Message);
            }

            double percentageAbovePoint7 = (double)countAbovePoint7 / measurements.Count * 100;

            Console.WriteLine();
            string resultAbovePoint7 = $"Процент превышений СКО выше 0.7: {percentageAbovePoint7:F3}%";
            Console.WriteLine(resultAbovePoint7);
            results.Add(resultAbovePoint7);

            int totalMeasurementTime = measurements.Count;
            string totalMeasurementTimeMessage = $"Общее время измерения сигнала: {totalMeasurementTime} секунд";
            Console.WriteLine(totalMeasurementTimeMessage);
            results.Add(totalMeasurementTimeMessage);

            double averageStdDev = stdDevs.Average();
            string averageStdDevMessage = $"Среднее значение СКО начиная с {startSeconds} секунд: {averageStdDev:F3}";
            Console.WriteLine(averageStdDevMessage);
            results.Add(averageStdDevMessage);

            CalculateSignalDifference(measurements, results);

            while (true)
            {
                Console.WriteLine("\nВведите начальный и конечный индекс измерений (или нажмите Enter для выхода):");

                Console.Write("Начальный индекс: ");
                string startInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(startInput)) break;

                Console.Write("Конечный индекс: ");
                string endInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(endInput)) break;

                if (int.TryParse(startInput, out int startIndex) && int.TryParse(endInput, out int endIndex))
                {
                    if (startIndex < 0 || startIndex >= measurements.Count || startIndex > endIndex)
                    {
                        Console.WriteLine("Ошибка: некорректные индексы.");
                        continue;
                    }

                    if (endIndex < 0 || endIndex >= measurements.Count)
                    {
                        Console.WriteLine("Ошибка: некорректные индексы.");
                        continue;
                    }

                    var periodMeasurements = measurements.GetRange(startIndex, endIndex - startIndex + 1);
                    double maxSignal = periodMeasurements.Max(m => m.Signal);
                    double minSignal = periodMeasurements.Min(m => m.Signal);
                    double difference = maxSignal - minSignal;

                    Console.Write($"Разница максимального и минимального значения сигнала: ");
                    if (difference > 30)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if (difference > 15)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    string differenceResult = $"{difference:F3}";
                    Console.WriteLine(differenceResult);
                    results.Add($"Разница максимального и минимального значения сигнала: {differenceResult}");
                    Console.ResetColor();

                    string maxResult = $"Максимум: {maxSignal:F3}, Время: {periodMeasurements.First(m => m.Signal == maxSignal).DateTime}, Строка: {measurements.IndexOf(periodMeasurements.First(m => m.Signal == maxSignal)) + 1}";
                    string minResult = $"Минимум: {minSignal:F3}, Время: {periodMeasurements.First(m => m.Signal == minSignal).DateTime}, Строка: {measurements.IndexOf(periodMeasurements.First(m => m.Signal == minSignal)) + 1}";
                    Console.WriteLine(maxResult);
                    Console.WriteLine(minResult);
                    results.Add(maxResult);
                    results.Add(minResult);
                }
                else
                {
                    Console.WriteLine("Ошибка: введены некорректные значения.");
                }
            }

            SaveResultsToFile(Path.GetFileNameWithoutExtension(filePath), results);
        }

        static void Main(string[] args)
        {
            Console.Write("Хотите пропустить открытие и работу с первым файлом? (да/нет): ");
            string skipFirstFileInput = Console.ReadLine();
            bool skipFirstFile = skipFirstFileInput.Equals("да", StringComparison.OrdinalIgnoreCase);

            bool processedFirstFile = false;

            if (!skipFirstFile)
            {
                string filePath = null;

                List<string> txtFiles = GetTxtFilesInSignalFolder();
                if (txtFiles.Count > 0)
                {
                    Console.WriteLine("Найдены следующие файлы в папке 'Signal files':");
                    for (int i = 0; i < txtFiles.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}. {Path.GetFileName(txtFiles[i])}");
                    }

                    Console.Write("Введите номер файла для выбора: ");
                    if (int.TryParse(Console.ReadLine(), out int fileIndex) && fileIndex > 0 && fileIndex <= txtFiles.Count)
                    {
                        filePath = txtFiles[fileIndex - 1];
                    }
                    else
                    {
                        Console.WriteLine("Некорректный выбор. Переход к ручному вводу пути к файлу.");
                    }
                }

                while (filePath == null)
                {
                    Console.Write("Введите путь к файлу: ");
                    filePath = Console.ReadLine();

                    if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    {
                        break;
                    }

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Файл не найден или путь некорректен. Попробуйте ещё раз.");
                    Console.ResetColor();
                }

                Console.WriteLine($"Файл успешно найден: {filePath}");

                ProcessFirstFile(filePath);
                processedFirstFile = true;
            }

            if (processedFirstFile)
            {
                Console.Write("Хотите пропустить работу со вторым файлом? (да/нет): ");
                string skipSecondFileInput = Console.ReadLine();
                bool skipSecondFile = skipSecondFileInput.Equals("да", StringComparison.OrdinalIgnoreCase);

                if (skipSecondFile)
                {
                    Console.WriteLine("Работа со вторым файлом пропущена.");
                    return;
                }
            }

            string secondFilePath = GetSavedFilePath("secondFilePath");
            if (secondFilePath == null || !File.Exists(secondFilePath))
            {
                while (true)
                {
                    Console.Write("Введите путь ко второму файлу: ");
                    secondFilePath = Console.ReadLine();

                    if (!string.IsNullOrWhiteSpace(secondFilePath) && File.Exists(secondFilePath))
                    {
                        SaveFilePath("secondFilePath", secondFilePath);
                        break;
                        static List<string> ReadFileLines(string filePath)
                        {
                            try
                            {
                                return File.ReadAllLines(filePath).ToList();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка при чтении файла: {ex.Message}");
                                return new List<string>();
                            }
                        }

                        static bool TryParseMeasurement(string line, out Measurement measurement)
                        {
                            measurement = null;
                            string pattern = @"^(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<time>\d{2}:\d{2}:\d{2})\s+(?<signal>[-+]?\d+,\d+)";
                            Regex regex = new Regex(pattern);
                            Match match = regex.Match(line);

                            if (match.Success)
                            {
                                string dateStr = match.Groups["date"].Value;
                                string timeStr = match.Groups["time"].Value;
                                string signalStr = match.Groups["signal"].Value.Replace(',', '.');

                                if (DateTime.TryParseExact(dateStr + " " + timeStr, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)
                                    && double.TryParse(signalStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double signalVal))
                                {
                                    measurement = new Measurement { DateTime = dt, Signal = signalVal };
                                    return true;
                                }
                            }

                            return false;
                        }

                        static void ProcessFirstFile(string filePath)
                        {
                            var lines = ReadFileLines(filePath);
                            if (lines.Count < 2)
                            {
                                Console.WriteLine("Файл не содержит измерений.");
                                return;
                            }

                            lines.RemoveAt(0);

                            List<Measurement> measurements = new List<Measurement>();

                            foreach (var line in lines)
                            {
                                if (TryParseMeasurement(line, out Measurement measurement))
                                {
                                    measurements.Add(measurement);
                                }
                                else
                                {
                                    Console.WriteLine($"Ошибка при парсинге строки: {line}");
                                }
                            }

                            // Остальная часть метода ProcessFirstFile...
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Файл не найден или путь некорректен. Попробуйте ещё раз.");
                    Console.ResetColor();
                }
            }

            ProcessSecondFile(secondFilePath);
            Console.ReadLine();
        }
    }
}
