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
            (this.DataContext as MainViewModel).CodeEditor = textEditor;
            (this.DataContext as MainViewModel).TreeText = treeView;
            (this.DataContext as MainViewModel).CodegenText = codegenView;
            textEditor.ShowLineNumbers = true;
        }
    }
}
