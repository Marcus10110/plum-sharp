using System;
using System.Linq;

namespace Saleae
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string settings_path = System.IO.Directory.GetCurrentDirectory() + System.IO.Path.DirectorySeparatorChar + "plumsettings.json";
            Plum plum = new Plum();
            if (plum.LoadData(settings_path) == false)
            {
                plum.ScanForDevices("", ""); //set username and password here at least once.
                plum.SaveData(settings_path);
            }

            Console.WriteLine("Current light levels: " + String.Join(", ", plum.GetAllLights().Select(x => x.ToString("0.#")).ToArray()));

            Random rand = new Random();
            double new_value = rand.NextDouble() * 100;

            plum.SetAllLights( new_value );
            System.Threading.Thread.Sleep(2000); //My lights are set to take 2 seconds to apply.
            Console.WriteLine("New light levels: " + String.Join(", ", plum.GetAllLights().Select(x => x.ToString("0.#")).ToArray()));


        }
    }
}
