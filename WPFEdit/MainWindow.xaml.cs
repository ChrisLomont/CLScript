using System.ComponentModel;
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
            var mv = DataContext as MainViewModel;
            if (mv == null)
            {
                MessageBox.Show("ERROR: cannot cast DataContext");
                return;
            }
            mv.CodeEditor = TextEditor;
            mv.TreeText = TreeView;
            mv.CodegenText = CodegenView;
            mv.SymbolText = SymbolView;
            mv.TraceText = TraceView;
            TextEditor.ShowLineNumbers = true;

            mv.Loaded();

        }

        void OnClosing(object sender, CancelEventArgs e)
        {
            (DataContext as MainViewModel).Closing();
        }
    }
}
