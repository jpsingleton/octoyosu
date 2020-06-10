// © James Singleton. EUPL-1.2 (see the LICENSE file for the full license governing this code).

using CsvHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace octoyosu
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var sw = Stopwatch.StartNew();

                var readingsPath = "detailedReadings.csv";
                if (args.Length > 0)
                {
                    readingsPath = args[0];
                }

                var pricingPath =
                    Directory.GetFiles(".", "csv_agile_*.csv", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault();
                if (args.Length > 1)
                {
                    pricingPath = args[1];
                }

                const decimal sgUr = 0.15288m;
                const decimal sgSc = 0.2163m;

                const decimal aoSc = 0.21m;

                Console.WriteLine();
                Console.WriteLine("🐙 🐙 🐙 🐙 🐙 🐙 🐙 🐙 🐙 🐙 🐙 🐙 🐙 🐙 🐙");
                Console.WriteLine();

                var rates = new Dictionary<DateTime, decimal>();
                var usages = new List<Usage>();

                using (var reader = new StreamReader(readingsPath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Read();
                    csv.ReadHeader();
                    var usage = new Usage
                    {
                        KWh = 0,
                        Time = DateTime.UnixEpoch,
                    };
                    while (csv.Read())
                    {
                        var kwh = csv.GetField<decimal>(3);
                        if (kwh <= 0) continue;

                        // Local time, not UTC - e.g. gap at 20200329 00:45
                        var time = DateTime.ParseExact(csv.GetField<string>(0), "yyyyMMdd HH:mm",
                            CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal).ToUniversalTime();

                        if (time.Minute != 00 && time.Minute != 30)
                        {
                            usage.KWh += kwh;
                            continue;
                        }

                        usage = new Usage
                        {
                            KWh = kwh,
                            Time = time,
                        };
                        usages.Add(usage);
                    }
                }

                var min = usages.Min(u => u.Time);
                var max = usages.Max(u => u.Time);
                var totalDays = (decimal) (max - min).TotalDays;
                Console.WriteLine($"{min:ddd dd MMM yyyy} to {max:ddd dd MMM yyyy} ({totalDays:0} days)");

                var totalKwh = usages.Sum(u => u.KWh);
                PrintAverages(totalKwh, totalDays, "kWh");

                var sgTotalUnitCost = totalKwh * sgUr;
                var sgTotalStandingCharge = totalDays * sgSc;
                var sgTotalCostGbpIncVat = sgTotalUnitCost + sgTotalStandingCharge;
                Console.WriteLine("Super Green:");
                PrintAverages(sgTotalCostGbpIncVat, totalDays, "GBP inc. VAT");

                Console.WriteLine("Loading agile pricing and calculating...");
                Console.WriteLine();

                using (var reader = new StreamReader(pricingPath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    while (csv.Read())
                    {
                        var time = DateTime.Parse(csv.GetField<string>(0)); // UTC
                        if (time >= min && time <= max)
                        {
                            rates.Add(time, csv.GetField<decimal>(4)); // inc. VAT
                        }
                    }
                }

                var aoTotalUnitCost = (
                    from usage in usages
                    let rate = rates.SingleOrDefault(r =>
                        r.Key == usage.Time)
                    select rate.Value * 0.01m * usage.KWh
                ).Sum();

                var aoTotalStandingCharge = totalDays * aoSc;
                var aoTotalCostIncVat = aoTotalUnitCost + aoTotalStandingCharge;
                Console.WriteLine("Agile:");
                PrintAverages(aoTotalCostIncVat, totalDays, "GBP inc. VAT");

                Console.WriteLine("Savings:");
                PrintAverages(sgTotalCostGbpIncVat - aoTotalCostIncVat, totalDays, "GBP inc. VAT");

                var agilePercentage = aoTotalCostIncVat / sgTotalCostGbpIncVat * 100;
                Console.WriteLine("Super Green 100%: 🐙🐙🐙🐙🐙🐙🐙🐙🐙🐙");
                Console.Write($"      Agile  {agilePercentage:0}%: ");
                for (var i = 10; i < agilePercentage; i += 10)
                {
                    Console.Write("🐙");
                }

                Console.WriteLine();
                Console.WriteLine();
                sw.Stop();

#if DEBUG
                Console.WriteLine($"Done in {sw.ElapsedMilliseconds:#,###}ms");
#endif
            }
            catch (FileNotFoundException fileNotFoundException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Could not find file: {fileNotFoundException.FileName}");
                Console.WriteLine("Usage: ./octoyosu [readingsFile.csv] [pricingFile.csv]");
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"An unexpected error occurred: {exception}");
            }

            Console.ResetColor();
        }

        private static void PrintAverages(decimal total, decimal days, string unit)
        {
            var day = total / days;
            var year = day * 365; // ignore leap year
            var month = year / 12;
            Console.WriteLine($"Period total {total:0.00} {unit} (approx)");
            Console.WriteLine($"Daily average {day:0.00} {unit} (approx)");
            Console.WriteLine($"Yearly average {year:0} {unit} (approx)");
            Console.WriteLine($"Monthly average {month:0} {unit} (approx)");
            Console.WriteLine();
        }
    }
}
