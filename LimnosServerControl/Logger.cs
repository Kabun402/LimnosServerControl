namespace LimnosServerControl
{
    public class Logger
    {
        public static void Log(string message, string service)
        {
            Console.WriteLine($"{DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")} | {service} - {message}");
            if (!Directory.Exists("./logs"))
                Directory.CreateDirectory("./logs");
            if (!Directory.Exists($"./logs/{service}"))
                Directory.CreateDirectory($"./logs/{service}");
            File.AppendAllText($"./logs/{service}/{service}Log_{DateTime.Now.ToString("yyyy_MM_dd")}.log", $"{DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")} - {message}\n");
        }
    }
}
