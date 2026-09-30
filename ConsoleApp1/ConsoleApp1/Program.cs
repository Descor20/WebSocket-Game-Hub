using ConsoleApp1.Objects.AppSection;

namespace ConsoleApp1;

class Program
{
    static async Task Main(string[] args)
    {
        GameManager.AddGames();
        var server = new Server(1);

        if (!server.Start())
        {
            Console.WriteLine("Le serveur est déjà démarré.");
            return;
        }

        Console.WriteLine("Server a un lobby");
        Console.WriteLine("Appuyez sur Entrée pour arrêter le serveur...");
        Console.ReadLine();

        server.Stop();
        Console.WriteLine("Serveur arrêté.");
    }
}