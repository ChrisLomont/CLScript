using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using ICSharpCode.AvalonEdit;
using Lomont.ClScript.CompilerLib;
using Microsoft.Win32;
using Environment = Lomont.ClScript.CompilerLib.Environment;

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
            SaveCommand = new RelayCommand(Save);
            LoadCommand = new RelayCommand(Load);
            ExportCommand = new RelayCommand(Export);
        }

        public RelayCommand CompileCommand { get; private set; }
        public RelayCommand SaveCommand { get; private set; }
        public RelayCommand LoadCommand { get; private set; }
        public RelayCommand ExportCommand { get; private set; }
        public TextEditor Editor { get; set; }
        public TextEditor TreeText { get; set; }
        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        public ObservableCollection<Token> Tokens { get; } = new ObservableCollection<Token>();
        
        void Save()
        {
            if (!string.IsNullOrEmpty(filename))
            {
                File.WriteAllText(filename, Editor.Text);
                Messages.Add($"File {filename} saved");
            }
            
        }

        string filename = "";
        void Load()
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                var text = File.ReadAllText(ofd.FileName);
                Editor.Text = text;
                filename = ofd.FileName;
                Messages.Add($"File {filename} loaded");
            }
        }

        void Export()
        {
            if (!string.IsNullOrEmpty(filename))
            {
                Compile();

                //compiler.todo();

                var localName = filename + ".export";
                Messages.Add($"Tokenized file {localName} saved");
            }
        }

        Compiler compiler;

        void Compile()
        {
            try
            {
                var output = new StringWriter();
                var text = Editor.Text;
                compiler = new CompilerLib.Compiler();
                string goldText;
                compiler.Compile(text, new Environment(output), out goldText);
                File.WriteAllText("goldOut.txt", goldText);
                var msgs = output.ToString().Split('\n');
                Messages.Clear();
                foreach (var msg in msgs)
                    Messages.Add(msg.Replace("\r", "").Replace("\n", ""));

                TreeText.Text = compiler.SyntaxTreeToText();
                Tokens.Clear();
                foreach (var t in compiler.GetTokens())
                    Tokens.Add(t);
            }
            catch (Exception ex)
            {
                Messages.Clear();
                Messages.Add($"FATAL: Compiler leaked exception {ex}");
            }

        }

        // editor article at http://www.codeproject.com/Articles/42490/Using-AvalonEdit-WPF-Text-Editor



    }
}