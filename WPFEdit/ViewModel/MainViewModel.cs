using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ICSharpCode.AvalonEdit;

namespace Lomont.ClScript.WPFEdit.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            ////if (IsInDesignMode)
            ////{
            ////    // Code runs in Blend --> create design time data.
            ////}
            ////else
            ////{
            ////    // Code runs "for real"
            ////}
            /// 
            CompileCommand = new RelayCommand(Compile);
        }

        public RelayCommand CompileCommand { get; private set; }
        public TextEditor Editor { get; set; }
        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        void Compile()
        {
            var output = new StringWriter();
            var lines = Editor.Text.Split('\n');
            var compiler = new CompilerLib.Compiler();
            compiler.Compile(lines, output);
            var msgs = output.ToString().Split('\n');
            Messages.Clear();
            foreach (var msg in msgs)
                Messages.Add(msg);
        }

        // editor article at http://www.codeproject.com/Articles/42490/Using-AvalonEdit-WPF-Text-Editor



    }
}