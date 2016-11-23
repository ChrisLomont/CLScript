using System.Windows;
using Lomont.ClScript.WPFEdit.ViewModel;

namespace Lomont.ClScript.WPFEdit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            (this.DataContext as MainViewModel).Editor = textEditor;
            (this.DataContext as MainViewModel).TreeText = treeView;
            textEditor.ShowLineNumbers = true;
        }
    }
}
