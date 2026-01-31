using System.Windows;
using MyNetBoot.Client.ViewModels;

namespace MyNetBoot.Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Parol = PasswordBox.Password;
            }
        }
    }
}