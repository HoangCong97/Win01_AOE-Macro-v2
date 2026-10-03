using System.Diagnostics;
using System.Security.Principal;
using AOEKeyboardMacroPro.Services;

namespace AOEKeyboardMacroPro;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test-loading")
        {
            RunLoadingTest();
            return;
        }

        if (args.Length > 0 && args[0] == "--test-queue")
        {
            RunUnitQueueTest();
            return;
        }

        // Initialize global exception logger
        CrashLogger.Initialize();

        // Enable 1ms high precision timer resolution for Windows
        NativeMethods.TimeBeginPeriod(1);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());

        NativeMethods.TimeEndPeriod(1);
    }

    static void RunLoadingTest()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== BẮT ĐẦU KIỂM THỬ LOADING OCR SERVICE ===");
        using var service = new LoadingOcrService();
        Console.WriteLine($"Số lượng mẫu số nạp được: {service.LoadedTemplateCount}/10");
        Console.WriteLine($"Vùng quét mặc định LoadingBox: X={service.CropSettings.LoadingBox.X}, Y={service.CropSettings.LoadingBox.Y}, W={service.CropSettings.LoadingBox.Width}, H={service.CropSettings.LoadingBox.Height}");

        string cropsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "AOE loading", "crops");
        if (!Directory.Exists(cropsDir))
        {
            cropsDir = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AOE loading", "crops");
        }

        int passed = 0;
        int total = 0;

        if (Directory.Exists(cropsDir))
        {
            var files = Directory.GetFiles(cropsDir, "*.png").OrderBy(f => f).ToArray();
            total = files.Length;
            foreach (var file in files)
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                using var bmp = new System.Drawing.Bitmap(fs);
                var res = service.RecognizeFromBitmap(bmp, 0, 0, bmp.Width, bmp.Height);
                string expectedChar = Path.GetFileName(file);
                var m = System.Text.RegularExpressions.Regex.Match(expectedChar, @"(\d)\.png$");
                string exp = m.Success ? m.Groups[1].Value : "?";
                bool ok = (res.RawText == exp);
                if (ok) passed++;
                Console.WriteLine($"File: {Path.GetFileName(file)} -> Đọc: '{res.RawText}' (Kỳ vọng: '{exp}') => {(ok ? "PASS" : "FAIL")}");
            }
        }

        // Test ảnh ghép 100% (số 1 + 0 + 0)
        using var bmp100 = new System.Drawing.Bitmap(30, 12);
        using (var g = System.Drawing.Graphics.FromImage(bmp100))
        {
            g.Clear(System.Drawing.Color.Black);
            string f1 = Path.Combine(cropsDir, "crop_002_Crop 1.png");
            string f0 = Path.Combine(cropsDir, "crop_001_Crop 0.png");
            if (File.Exists(f1) && File.Exists(f0))
            {
                using var b1 = new System.Drawing.Bitmap(f1);
                using var b0 = new System.Drawing.Bitmap(f0);
                g.DrawImage(b1, 2, 1);
                g.DrawImage(b0, 7, 1);
                g.DrawImage(b0, 15, 1);
            }
        }

        var res100 = service.RecognizeFromBitmap(bmp100, 0, 0, 30, 12);
        bool test100Ok = (res100.Percentage == 100 && res100.IsComplete);
        Console.WriteLine($"Ảnh ghép 100% -> Đọc: {res100.Percentage}% | IsComplete: {res100.IsComplete} => {(test100Ok ? "PASS" : "FAIL")}");

        Console.WriteLine($"KẾT QUẢ: Đã pass {passed}/{total} chữ số đơn và ghép 100% {(test100Ok ? "thành công" : "thất bại")}.");
        Console.WriteLine("=== KẾT THÚC KIỂM THỬ ===");
    }

    static void RunUnitQueueTest()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== BẮT ĐẦU KIỂM THỬ UNIT QUEUE OCR SERVICE ===");
        using var service = new UnitQueueOcrService();
        Console.WriteLine($"Số lượng mẫu số nạp được: {service.LoadedTemplateCount}/10");
        Console.WriteLine($"Vùng quét mặc định QueueBox: X={service.CropSettings.QueueBox.X}, Y={service.CropSettings.QueueBox.Y}, W={service.CropSettings.QueueBox.Width}, H={service.CropSettings.QueueBox.Height}");

        string cropsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "AOE xoc quan", "crops");
        if (!Directory.Exists(cropsDir))
        {
            cropsDir = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AOE xoc quan", "crops");
        }

        int passed = 0;
        int total = 0;

        if (Directory.Exists(cropsDir))
        {
            var files = Directory.GetFiles(cropsDir, "*.png").OrderBy(f => f).ToArray();
            total = files.Length;
            foreach (var file in files)
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                using var bmp = new System.Drawing.Bitmap(fs);
                var res = service.RecognizeFromBitmap(bmp, 0, 0, bmp.Width, bmp.Height);
                string expectedChar = Path.GetFileName(file);
                var m = System.Text.RegularExpressions.Regex.Match(expectedChar, @"(\d)\.png$");
                string exp = m.Success ? m.Groups[1].Value : "?";
                bool ok = (res.RawText == exp);
                if (ok) passed++;
                Console.WriteLine($"File: {Path.GetFileName(file)} -> Đọc: '{res.RawText}' (Kỳ vọng: '{exp}') => {(ok ? "PASS" : "FAIL")}");
            }
        }

        // Test ảnh rộng 265px chứa số '12' (số 1 + 2)
        using var bmpWide = new System.Drawing.Bitmap(265, 16);
        using (var g = System.Drawing.Graphics.FromImage(bmpWide))
        {
            g.Clear(System.Drawing.Color.Black);
            string f1 = Path.Combine(cropsDir, "crop_005_1.png");
            string f2 = Path.Combine(cropsDir, "crop_006_2.png");
            if (File.Exists(f1) && File.Exists(f2))
            {
                using var b1 = new System.Drawing.Bitmap(f1);
                using var b2 = new System.Drawing.Bitmap(f2);
                g.DrawImage(b1, 20, 3);
                g.DrawImage(b2, 26, 3);
            }
        }

        var resWide = service.RecognizeFromBitmap(bmpWide, 0, 0, 265, 16);
        bool testWideOk = (resWide.PrimaryCount == 12 && resWide.RawText == "12");
        Console.WriteLine($"Ảnh rộng 265px chứa số '12' -> Đọc: Primary={resWide.PrimaryCount}, Raw='{resWide.RawText}' => {(testWideOk ? "PASS" : "FAIL")}");

        Console.WriteLine($"KẾT QUẢ: Đã pass {passed}/{total} chữ số đơn và test dải rộng 265px {(testWideOk ? "thành công" : "thất bại")}.");
        Console.WriteLine("=== KẾT THÚC KIỂM THỬ ===");
    }
}