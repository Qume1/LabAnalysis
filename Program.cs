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

        // Метод для расчета стандартного отклонения
        static double CalculateStdDev(List<double> values)
        {
            if (values.Count < 2) return 0; // Несмещенная оценка требует как минимум два значения
            double mean = values.Average();
            double sumSquares = values.Select(x => (x - mean) * (x - mean)).Sum();
            return Math.Sqrt(sumSquares / (values.Count - 1)); // Используем N-1 для несмещенной оценки
        }

        // Метод для сохранения результатов в файл
        static void SaveResultsToFile(string fileName, List<string> results)
        {
            string resultsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results");
            if (!Directory.Exists(resultsDirectory))
            {
                Directory.CreateDirectory(resultsDirectory);
            }

            string resultFilePath = Path.Combine(resultsDirectory, $"{fileName}.txt");

            // Append results to the file if it already exists
            if (File.Exists(resultFilePath))
            {
                File.AppendAllLines(resultFilePath, results);
            }
            else
            {
                File.WriteAllLines(resultFilePath, results);
            }

            Console.WriteLine($"\nРезультаты сохранены в файл: {resultFilePath}\n");
        }

        // Метод для получения списка текстовых файлов в папке "Signal files"
        static List<string> GetTxtFilesInSignalFolder()
        {
            string signalFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Signal files");
            if (Directory.Exists(signalFolder))
            {
                return Directory.GetFiles(signalFolder, "*.txt").ToList();
            }
            return new List<string>();
        }

        // Метод для получения сохраненного пути к файлу из конфигурационного файла
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

        // Метод для сохранения пути к файлу в конфигурационный файл
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

        // ...existing code...

        static void ProcessSecondFile(string firstFilePath)
        {
            string directory = Path.GetDirectoryName(firstFilePath);
            List<string> txtFiles = Directory.GetFiles(directory, "*.txt").ToList();

            if (txtFiles.Count == 0)
            {
                Console.WriteLine("\nНет доступных файлов для выбора.\n");
                return;
            }

            Console.WriteLine("\nНайдены следующие файлы в папке:");
            for (int i = 0; i < txtFiles.Count; i++)
            {
                Console.WriteLine($"\n{i + 1}. {Path.GetFileName(txtFiles[i])}");
            }

            Console.Write("\nВведите номер файла для выбора: ");
            if (!int.TryParse(Console.ReadLine(), out int fileIndex) || fileIndex <= 0 || fileIndex > txtFiles.Count)
            {
                Console.WriteLine("\nНекорректный выбор.\n");
                return;
            }

            string filePath = txtFiles[fileIndex - 1];
            var lines = File.ReadAllLines(filePath).Skip(3).ToList(); // Пропускаем первые три строки

            string pattern = @"(?<value>[-+]?\d+[.,]\d+)\s*(?<date>\d{2}\.\d{2}\.\d{4})\s*(?<time>\d{2}:\d{2}:\d{2})\s*(?<rangeStart>[-+]?\d+[.,]\d+)\s*-\s*(?<rangeEnd>[-+]?\d+[.,]\d+)";
            Regex regex = new Regex(pattern);

            List<double> values = new List<double>();
            List<DateTime> timestamps = new List<DateTime>();
            List<double> rangeStarts = new List<double>();
            List<double> rangeEnds = new List<double>();

            lines.Reverse();

            foreach (var line in lines)
            {
                Match match = regex.Match(line);
                if (match.Success)
                {
                    string dateStr = match.Groups["date"].Value;
                    string timeStr = match.Groups["time"].Value;
                    string valueStr = match.Groups["value"].Value.Replace(',', '.');
                    string rangeStartStr = match.Groups["rangeStart"].Value.Replace(',', '.');
                    string rangeEndStr = match.Groups["rangeEnd"].Value.Replace(',', '.');

                    if (DateTime.TryParseExact(dateStr + " " + timeStr, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)
                        && double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double value)
                        && double.TryParse(rangeStartStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double rangeStart)
                        && double.TryParse(rangeEndStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double rangeEnd))
                    {
                        values.Add(value);
                        timestamps.Add(dt);
                        rangeStarts.Add(rangeStart);
                        rangeEnds.Add(rangeEnd);
                    }
                }
            }

            if (values.Count < 5)
            {
                Console.WriteLine("\nНедостаточно данных для расчёта.\n");
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

                double minRange = rangeStarts[i];
                double maxRange = rangeEnds[i + 4];

                if (stdDev > 0.2)
                {
                    countAbovePoint2++;
                    Console.ForegroundColor = ConsoleColor.Red;
                }

                string result = $"Предел обнаружения: {stdDev:F3} {timeInterval}, Интервал: {minRange:F2} - {maxRange:F2}";
                Console.WriteLine(result);
                results.Add(result);
                Console.ResetColor();
                totalCount++;
            }

            double percentageAbovePoint2 = (double)countAbovePoint2 / totalCount * 100;
            string percentageResult = $"\nПроцент превышений Предела обнаружения выше 0.2: {percentageAbovePoint2:F3}%\n";
            Console.WriteLine(percentageResult);
            results.Add(percentageResult);

            string outputFileName = $"{Path.GetFileNameWithoutExtension(filePath)} Расчет реальных проб";
            SaveResultsToFile(outputFileName, results);
        }

        // Метод для расчета разницы сигнала
        static void CalculateSignalDifference(List<Measurement> measurements, List<string> results)
        {
            int startIndex = measurements.FindIndex(m => (m.DateTime - measurements[0].DateTime).TotalSeconds >= 1800);
            int endIndex = measurements.FindIndex(m => (m.DateTime - measurements[0].DateTime).TotalSeconds >= 3600);

            if (startIndex == -1 || endIndex == -1 || startIndex >= endIndex)
            {
                Console.WriteLine("\nНедостаточно данных для автоматического расчета диапазона.\n");
                return;
            }

            var periodMeasurements = measurements.GetRange(startIndex, endIndex - startIndex + 1);
            double maxSignal = periodMeasurements.Max(m => m.Signal);
            double minSignal = periodMeasurements.Min(m => m.Signal);
            double difference = maxSignal - minSignal;

            Console.Write($"\nАвтоматически рассчитанная разница максимального и минимального значения сигнала (1800-3600 секунд): ");
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

        // ...existing code...

        // Modify the ProcessFirstFile method to remove the output of standard deviation (СКО)
        // Modify the ProcessFirstFile method to remove the output of standard deviation (СКО)
        // Only display the average value and percentage in the ProcessFirstFile method
        static void ProcessFirstFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath).ToList();
            if (lines.Count < 2)
            {
                Console.WriteLine("\nФайл не содержит измерений.\n");
                return;
            }

            lines.RemoveAt(0);

            // Регулярное выражение для извлечения даты, времени и значения сигнала с запятой
            string pattern = @"^(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<time>\d{2}:\d{2}:\d{2})\s+(?<signal>[-+]?\d+[.,]\d+)";
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
                        Console.WriteLine($"\nОшибка при парсинге сигнала: {signalStr}\n");
                    }
                }
            }

            if (measurements.Count < 30)
            {
                Console.WriteLine("\nНедостаточно данных для расчёта по 30 измерениям.\n");
                return;
            }

            Console.Write("\nВведите начальную строку (в секундах) для расчета процента: ");
            if (!double.TryParse(Console.ReadLine(), out double startSeconds))
            {
                Console.WriteLine("\nНекорректное значение. Используется значение по умолчанию: 1800\n");
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

                    if (stdDev > 0.7)
                    {
                        countAbovePoint7++;
                    }
                    else
                    {
                        countInRange++;
                    }
                }
            }

            double percentageAbovePoint7 = (double)countAbovePoint7 / measurements.Count * 100;

            Console.WriteLine();
            string resultAbovePoint7 = $"\nПроцент превышений СКО выше 0.7: {percentageAbovePoint7:F3}%\n";
            Console.WriteLine(resultAbovePoint7);
            results.Add(resultAbovePoint7);

            int totalMeasurementTime = measurements.Count;
            string totalMeasurementTimeMessage = $"\nОбщее время измерения сигнала: {totalMeasurementTime} секунд\n";
            Console.WriteLine(totalMeasurementTimeMessage);
            results.Add(totalMeasurementTimeMessage);

            double averageStdDev = stdDevs.Average();
            string averageStdDevMessage = $"\nСреднее значение СКО начиная с {startSeconds} секунд: {averageStdDev:F3}\n";
            Console.WriteLine(averageStdDevMessage);
            results.Add(averageStdDevMessage);

            CalculateSignalDifference(measurements, results);

            while (true)
            {
                Console.WriteLine("\nВведите начальный и конечный индекс измерений (или нажмите Enter для выхода):");

                Console.Write("\nНачальный индекс: ");
                string startInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(startInput)) break;

                Console.Write("\nКонечный индекс: ");
                string endInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(endInput)) break;

                if (int.TryParse(startInput, out int startIndex) && int.TryParse(endInput, out int endIndex))
                {
                    if (startIndex < 0 || startIndex >= measurements.Count || startIndex > endIndex)
                    {
                        Console.WriteLine("\nОшибка: некорректные индексы.\n");
                        continue;
                    }

                    if (endIndex < 0 || endIndex >= measurements.Count)
                    {
                        Console.WriteLine("\nОшибка: некорректные индексы.\n");
                        continue;
                    }

                    var periodMeasurements = measurements.GetRange(startIndex, endIndex - startIndex + 1);
                    double maxSignal = periodMeasurements.Max(m => m.Signal);
                    double minSignal = periodMeasurements.Min(m => m.Signal);
                    double difference = maxSignal - minSignal;

                    Console.Write($"\nРазница максимального и минимального значения сигнала: ");
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
                    Console.WriteLine("\nОшибка: введены некорректные значения.\n");
                }
            }

            SaveResultsToFile(Path.GetFileNameWithoutExtension(filePath), results);

            // Call ProcessVirtualSamplesFile after processing the first file
            Console.Write("Введите калибровочный коэффициент A(коэффициент градуировки) (по умолчанию 252.1): ");
            if (!double.TryParse(Console.ReadLine(), out double divisor))
            {
                Console.WriteLine("Некорректное значение. Используется значение по умолчанию: 252.1");
                divisor = 252.1;
            }

            ProcessVirtualSamplesFile(filePath, divisor, results);
        }

        // Метод для расчета стандартного отклонения и разницы
        static void CalculateStandardDeviationAndDifference()
        {
            string filePath = null;

            List<string> txtFiles = GetTxtFilesInSignalFolder();
            if (txtFiles.Count > 0)
            {
                Console.WriteLine("\nНайдены следующие файлы в папке 'Signal files':");
                for (int i = 0; i < txtFiles.Count; i++)
                {
                    Console.WriteLine($"\n{i + 1}. {Path.GetFileName(txtFiles[i])}");
                }

                Console.Write("\nВведите номер файла для выбора: ");
                if (int.TryParse(Console.ReadLine(), out int fileIndex) && fileIndex > 0 && fileIndex <= txtFiles.Count)
                {
                    filePath = txtFiles[fileIndex - 1];
                }
                else
                {
                    Console.WriteLine("\nНекорректный выбор. Переход к ручному вводу пути к файлу.\n");
                }
            }

            while (filePath == null)
            {
                Console.Write("\nВведите путь к файлу: ");
                filePath = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nФайл не найден или путь некорректен. Попробуйте ещё раз.\n");
                Console.ResetColor();
            }

            Console.WriteLine($"\nФайл успешно найден: {filePath}\n");

            ProcessFirstFile(filePath);
        }

        // Метод для расчета предела обнаружения
        static void CalculateLimitDetection()
        {
            string secondFilePath = GetSavedFilePath("secondFilePath");
            if (secondFilePath == null || !File.Exists(secondFilePath))
            {
                while (true)
                {
                    Console.Write("\nВведите путь ко второму файлу: ");
                    secondFilePath = Console.ReadLine();

                    if (!string.IsNullOrWhiteSpace(secondFilePath) && File.Exists(secondFilePath))
                    {
                        SaveFilePath("secondFilePath", secondFilePath);
                        break;
                    }

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nФайл не найден или путь некорректен. Попробуйте ещё раз.\n");
                    Console.ResetColor();
                }
            }

            ProcessSecondFile(secondFilePath);
        }

        // Метод для расчета площади вокруг кривой
        static double CalculateSignedArea(List<Measurement> measurements, double regressionLine)
        {
            double area = 0;

            for (int i = 1; i < measurements.Count; i++)
            {
                double y1 = measurements[i - 1].Signal - regressionLine;
                double y2 = measurements[i].Signal - regressionLine;

                // Метод трапеций: (y1 + y2) / 2 * dx, где dx = 1
                double trapezoidArea = (y1 + y2) / 2;

                area += trapezoidArea;
            }

            return area;
        }



        // Метод для расчета линии регрессии по двум точкам
        static double CalculateRegressionLine(List<Measurement> beforeSegment, List<Measurement> firstSegment, List<Measurement> lastSegment, List<Measurement> afterSegment)
        {
            double AverageOrZero(List<Measurement> segment) => segment.Count > 0 ? segment.Average(m => m.Signal) : 0;

            double firstPointY = (AverageOrZero(beforeSegment) + AverageOrZero(firstSegment)) / 2;
            double secondPointY = (AverageOrZero(lastSegment) + AverageOrZero(afterSegment)) / 2;

            double firstPointX = 0;
            double secondPointX = 1;

            double slope = (secondPointY - firstPointY) / (secondPointX - firstPointX);
            double intercept = firstPointY - slope * firstPointX;

            return slope * 0.5 + intercept;
        }


        // Update the ProcessVirtualSamplesFile method to accept a results list parameter and append results to it instead of saving to a separate file
        static void ProcessVirtualSamplesFile(string filePath, double divisor, List<string> results)
        {
            var lines = File.ReadAllLines(filePath).ToList();
            if (lines.Count < 1800 + 70)
            {
                Console.WriteLine("Файл не содержит достаточного количества измерений.");
                return;
            }

            lines.RemoveAt(0);

            // Регулярное выражение для извлечения даты, времени и значения сигнала с запятой
            string pattern = @"^(?<date>\d{2}\.\d{2}\.\d{4})\s+(?<time>\d{2}:\d{2}:\d{2})\s+(?<signal>[-+]?\d+[.,]\d+)";
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

            if (measurements.Count < 1800 + 70)
            {
                Console.WriteLine("Недостаточно данных для расчёта по 60 измерениям начиная с 1800 строки.");
                return;
            }

            List<double> areas = new List<double>();
            int countAbovePoint2 = 0;
            int totalCount = 0;

            for (int i = 1800; i <= measurements.Count - 60; i += 70) // 60 lines segment with 10 lines gap
            {
                var segment = measurements.Skip(i).Take(60).ToList();
                var beforeSegment = measurements.Skip(i - 5).Take(5).ToList();
                var firstSegment = measurements.Skip(i).Take(5).ToList();
                var lastSegment = measurements.Skip(i + 55).Take(5).ToList();
                var afterSegment = measurements.Skip(i + 60).Take(5).ToList();

                double regressionLine = CalculateRegressionLine(beforeSegment, firstSegment, lastSegment, afterSegment);
                double area = CalculateSignedArea(segment, regressionLine) / divisor;
                areas.Add(area);
            }

            for (int i = 0; i <= areas.Count - 5; i += 5)
            {
                var group = areas.Skip(i).Take(5).ToList();
                if (group.Count == 5)
                {
                    double stdDev = CalculateStdDev(group) * 3;
                    if (stdDev > 0.2)
                    {
                        countAbovePoint2++;
                        Console.ForegroundColor = ConsoleColor.Red;
                    }

                    // Calculate the time interval for the group
                    DateTime startTime = measurements[i * 60].DateTime;
                    DateTime endTime = measurements[(i + 4) * 60 + 59].DateTime;
                    string timeInterval = $"(с {startTime:HH:mm:ss} по {endTime:HH:mm:ss})";

                    string result = $"Предел обнаружения: {stdDev:F3} {timeInterval}";
                    Console.WriteLine(result);
                    results.Add(result);
                    Console.ResetColor();
                    totalCount++;
                }
            }

            double percentageAbovePoint2 = (double)countAbovePoint2 / totalCount * 100;
            Console.WriteLine($"Процент превышений предела обнаружения выше 0.2: {percentageAbovePoint2:F3}%");
            results.Add($"Процент превышений предела обнаружения выше 0.2: {percentageAbovePoint2:F3}%");

            SaveResultsToFile(Path.GetFileNameWithoutExtension(filePath), results);
        }

        // В методе CalculateVirtualSamples добавьте создание списка results и передайте его в ProcessVirtualSamplesFile
        static void CalculateVirtualSamples()
        {
            string filePath = null;

            List<string> txtFiles = GetTxtFilesInSignalFolder();
            if (txtFiles.Count > 0)
            {
                Console.WriteLine("\nНайдены следующие файлы в папке 'Signal files':");
                for (int i = 0; i < txtFiles.Count; i++)
                {
                    Console.WriteLine($"\n{i + 1}. {Path.GetFileName(txtFiles[i])}");
                }

                Console.Write("\nВведите номер файла для выбора: ");
                if (int.TryParse(Console.ReadLine(), out int fileIndex) && fileIndex > 0 && fileIndex <= txtFiles.Count)
                {
                    filePath = txtFiles[fileIndex - 1];
                }
                else
                {
                    Console.WriteLine("\nНекорректный выбор. Переход к ручному вводу пути к файлу.\n");
                }
            }

            while (filePath == null)
            {
                Console.Write("\nВведите путь к файлу: ");
                filePath = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nФайл не найден или путь некорректен. Попробуйте ещё раз.\n");
                Console.ResetColor();
            }

            Console.WriteLine($"\nФайл успешно найден: {filePath}\n");

            Console.Write("Введите значение для деления площади (по умолчанию 252.1): ");
            if (!double.TryParse(Console.ReadLine(), out double divisor))
            {
                Console.WriteLine("Некорректное значение. Используется значение по умолчанию: 252.1");
                divisor = 252.1;
            }

            List<string> results = new List<string>();
            ProcessVirtualSamplesFile(filePath, divisor, results);
        }

        // Метод для очистки конфигурационного файла
        static void ClearConfigFile()
        {
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
            if (File.Exists(configFilePath))
            {
                File.WriteAllText(configFilePath, string.Empty);
                Console.WriteLine("Файл config.txt успешно очищен.");
            }
            else
            {
                Console.WriteLine("Файл config.txt не найден.");
            }
        }

        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Выберите тип расчета:");
                Console.WriteLine("1. Расчет СКО и разницы");
                Console.WriteLine("2. Расчет предела обнаружения");
                Console.WriteLine("3. Выход");

                Console.Write("Введите номер выбора: ");
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        CalculateStandardDeviationAndDifference();
                        break;
                    case "2":
                        CalculateLimitDetection();
                        break;
                    case "3":
                        return;
                    default:
                        Console.WriteLine("Некорректный выбор.");
                        break;
                }

                Console.WriteLine();
            }
        }
    }
}
