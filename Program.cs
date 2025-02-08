using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        static void Main(string[] args)
        {
            string filePath = null;

            // Цикл для повторного ввода пути к файлу
            while (true)
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

            // Регулярное выражение для извлечения даты, времени и значения сигнала.
            // Регулярное выражение для извлечения даты, времени и значения сигнала с запятой
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

            // Считаем количество превышений СКО выше 0.7
            int countAbovePoint7 = 0;

            // Переменные для подсчета
            int countInRange = 0;

            Console.WriteLine("Результаты расчёта СКО:");

            for (int i = 29; i < measurements.Count; i++)
            {
                var window = measurements.Skip(i - 29).Take(30).ToList();
                List<double> signals = window.Select(m => m.Signal).ToList();
                double stdDev = CalculateStdDev(signals);

                // Печатаем дату, время и количество секунд с начала измерения, если СКО больше 0.5
                if (stdDev > 0.5 || stdDev > 0.7)
                {
                    var lastMeasurement = measurements[i];
                    TimeSpan timeSinceStart = lastMeasurement.DateTime - measurements[0].DateTime;

                    // Если СКО больше 0.7, увеличиваем счетчик
                    if (stdDev > 0.7)
                    {
                        countAbovePoint7++;
                        // Выводим СКО с красным фоном
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{lastMeasurement.DateTime.ToString("dd.MM.yyyy HH:mm:ss")} ({timeSinceStart.TotalSeconds:F0} секунд) - СКО: {stdDev:F3}");
                        Console.ResetColor();
                    }
                    else if (stdDev > 0.5 && stdDev <= 0.7)
                    {
                        countInRange++;
                        // Выводим СКО без изменения цвета
                        Console.WriteLine($"{lastMeasurement.DateTime.ToString("dd.MM.yyyy HH:mm:ss")} ({timeSinceStart.TotalSeconds:F0} секунд) - СКО: {stdDev:F3}");
                    }
                }
            }

            // Расчет процента превышений
            double percentageAbovePoint7 = (double)countAbovePoint7 / measurements.Count * 100;
            double percentageInRange = (double)countInRange / measurements.Count * 100;

            // Выводим результат
            Console.WriteLine();
            Console.WriteLine($"Процент превышений СКО выше 0.7: {percentageAbovePoint7:F2}%");
            Console.WriteLine($"Процент превышений СКО в пределах от 0.5 до 0.7: {percentageInRange:F2}%");

            Console.WriteLine("Введите начальный индекс (например, 1800):");
            int startIndex = int.Parse(Console.ReadLine() ?? "0");

            Console.WriteLine("Введите конечный индекс (например, 3600):");
            int endIndex = int.Parse(Console.ReadLine() ?? "0");

            // Расчёт разницы между максимальным и минимальным значениями сигнала
            if (startIndex >= measurements.Count)
            {
                Console.WriteLine("Недостаточно данных для расчёта для заданного периода.");
            }
            else
            {
                var periodMeasurements = measurements.GetRange(startIndex, endIndex - startIndex + 1);
                double maxSignal = periodMeasurements.Max(m => m.Signal);
                double minSignal = periodMeasurements.Min(m => m.Signal);
                double difference = maxSignal - minSignal;

                int maxIndex = startIndex + periodMeasurements.FindIndex(m => m.Signal == maxSignal) + 1;
                int minIndex = startIndex + periodMeasurements.FindIndex(m => m.Signal == minSignal) + 1;

                // Цветной вывод числового значения разницы в зависимости от величины разницы
                Console.Write($"Разница максимального и минимального значения сигнала: ");
                if (difference > 30)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                else if (difference > 15)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                Console.WriteLine($"{difference:F3}");

                // Сброс цвета после вывода
                Console.ResetColor();

                Console.WriteLine($"Максимум: {maxSignal:F3}, Время: {periodMeasurements.First(m => m.Signal == maxSignal).DateTime}, Строка: {maxIndex}");
                Console.WriteLine($"Минимум: {minSignal:F3}, Время: {periodMeasurements.First(m => m.Signal == minSignal).DateTime}, Строка: {minIndex}");
            }

        }
    }
}
