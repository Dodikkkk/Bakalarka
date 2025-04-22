using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.IO;
using System.Threading.Tasks;
using Avalonia;

namespace AvaloniaApplication1
{
    public partial class WelcomeWindow : Window
    {
        //staticka premenna s nazvom suboru, nech sa o nem moze dozvediet druhe okno
        public static string? fileName;

        public WelcomeWindow()
        {
            InitializeComponent();
            ChooseAFileButton.Click += ChooseAFileButton_Click;     //na kliku tlacidla sa zavola dana metoda
        }

        //metoda, ktora sa vykona, ked sa stlaci tlacidlo
        private async void ChooseAFileButton_Click(object? sender, RoutedEventArgs e)
        {
            //otvori prehliadac na vybranie suboru
            var openFileDialog = new OpenFileDialog
            {
                Title = "Open .gpx file",
                AllowMultiple = false,
                Filters =
                {
                    new FileDialogFilter { Name = ".gpx files", Extensions = { "gpx" } },   //umoznuje vybrat iba .gpx subory
                }
            };
            
            //nazov suboru sa ulozi do premennej a otvori sa hlavne okno. ak sa subor nenajde, nic sa nestane
            string[] result = await openFileDialog.ShowAsync(this);
            if (result.Length == 0)
            {
                return;
            }
            fileName = result[0];
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
    }
}