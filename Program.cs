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
            if (values.Count == 0) return 0;
            double mean = values.Average();
            double sumSquares = values.Select(x => (x - mean) * (x - mean)).Sum();
            return Math.Sqrt(sumSquares / values.Count);
        }

        static void SaveResultsToFile(string fileName, List<string> results)
        {
            string resultsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results");
            if (!Directory.Exists(resultsDirectory))
            {
                Directory.CreateDirectory(resultsDirectory);
            }

            // Форматируем текущую дату и время для использования в названии файла
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

        static void Main(string[] args)
        {
            string filePath = null;

            // Поиск TXT файлов в папке "Signal files"
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

            // Цикл для повторного ввода пути к файлу, если файл не был выбран из списка
            while (filePath == null)
            {
                Console.Write("Введите путь к файлу: ");
                filePath = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    break; // Если файл найден, выходим из цикла
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Файл не найден или путь некорректен. Попробуйте ещё раз.");
                Console.ResetColor();
            }

            // Далее обработка файла
            Console.WriteLine($"Файл успешно найден: {filePath}");

            var lines = File.ReadAllLines(filePath).ToList();
            if (lines.Count < 2)
            {
                Console.WriteLine("Файл не содержит измерений.");
                return;
            }

            lines.RemoveAt(0); // Удаляем строку заголовка

            // Регулярное выражение для извлечения даты, времени и значения сигнала с запятой
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


                    if (DateTime.TryParseExact(dateStr + " " + timeStr, "dd.MM.yyyy HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)
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

            // Ввод пользователем минимального значения СКО для вывода
            Console.Write("Введите минимальное значение СКО для вывода: ");
            if (!double.TryParse(Console.ReadLine(), out double minStdDev))
            {
                Console.WriteLine("Некорректное значение. Используется значение по умолчанию: 0.7");
                minStdDev = 0.7;
            }

            // Ввод пользователем начальной строки для расчета процента
            Console.Write("Введите начальную строку (в секундах) для расчета процента: ");
            if (!double.TryParse(Console.ReadLine(), out double startSeconds))
            {
                Console.WriteLine("Некорректное значение. Используется значение по умолчанию: 0");
                startSeconds = 0;
            }

            // Считаем количество превышений СКО выше 0.7
            int countAbovePoint7 = 0;

            // Переменные для подсчета
            int countInRange = 0;

            // Флаг для отслеживания значений СКО выше 0.5
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

                // Печатаем дату, время и количество секунд с начала измерения, если СКО выше указанного порога и время больше начального времени
                if (timeSinceStart.TotalSeconds >= startSeconds)
                {
                    stdDevs.Add(stdDev);

                    if (stdDev > minStdDev)
                    {
                        hasStdDevAbovePoint5 = true;

                        // Если СКО больше 0.7, увеличиваем счетчик
                        if (stdDev > 0.7)
                        {
                            countAbovePoint7++;
                            // Выводим СКО с красным фоном
                            Console.ForegroundColor = ConsoleColor.Red;
                            string result = $"{lastMeasurement.DateTime.ToString("dd.MM.yyyy HH:mm:ss")} ({timeSinceStart.TotalSeconds:F0} секунд) - СКО: {stdDev:F3}";
                            Console.WriteLine(result);
                            results.Add(result);
                            Console.ResetColor();
                        }
                        else
                        {
                            countInRange++;
                            // Выводим СКО без изменения цвета
                            string result = $"{lastMeasurement.DateTime.ToString("dd.MM.yyyy HH:mm:ss")} ({timeSinceStart.TotalSeconds:F0} секунд) - СКО: {stdDev:F3}";
                            Console.WriteLine(result);
                            results.Add(result);
                        }
                    }
                }
            }

            // Если нет значений СКО выше minStdDev, выводим сообщение
            if (!hasStdDevAbovePoint5)
            {
                string noStdDevAbovePoint5Message = $"Нет значений СКО выше {minStdDev}";
                Console.WriteLine(noStdDevAbovePoint5Message);
                results.Add(noStdDevAbovePoint5Message);
            }

            // Расчет процента превышений
            double percentageAbovePoint7 = (double)countAbovePoint7 / measurements.Count * 100;

            // Выводим результат
            Console.WriteLine();
            string resultAbovePoint7 = $"Процент превышений СКО выше 0.7: {percentageAbovePoint7:F3}%";
            Console.WriteLine(resultAbovePoint7);
            results.Add(resultAbovePoint7);

            // Вывод общего времени измерения сигнала
            int totalMeasurementTime = measurements.Count;
            string totalMeasurementTimeMessage = $"Общее время измерения сигнала: {totalMeasurementTime} секунд";
            Console.WriteLine(totalMeasurementTimeMessage);
            results.Add(totalMeasurementTimeMessage);

            // Расчет и вывод среднего значения СКО начиная с указанного времени
            double averageStdDev = stdDevs.Average();
            string averageStdDevMessage = $"Среднее значение СКО начиная с {startSeconds} секунд: {averageStdDev:F3}";
            Console.WriteLine(averageStdDevMessage);
            results.Add(averageStdDevMessage);

            while (true)
            {
                Console.WriteLine("\nВведите начальный и конечный индекс измерений (или нажмите Enter для выхода):");

                Console.Write("Начальный индекс: ");
                string startInput = Console.ReadLine();

                // Проверка на выход
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

                    // Если записей меньше 3600, автоматически рассчитываем разницу от заданной до крайней существующей
                    if (measurements.Count < 3600)
                    {
                        endIndex = measurements.Count - 1;
                    }
                    else if (endIndex < 0 || endIndex >= measurements.Count)
                    {
                        Console.WriteLine("Ошибка: некорректные индексы.");
                        continue;
                    }

                    var periodMeasurements = measurements.GetRange(startIndex, endIndex - startIndex + 1);
                    double maxSignal = periodMeasurements.Max(m => m.Signal);
                    double minSignal = periodMeasurements.Min(m => m.Signal);
                    double difference = maxSignal - minSignal;

                    // Цветной вывод числового значения разницы
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

            Console.WriteLine("Программа завершена. Нажмите любую клавишу");
            Console.ReadLine();
        }
    }
}
