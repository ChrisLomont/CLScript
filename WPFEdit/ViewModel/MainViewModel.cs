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
using System.ComponentModel;

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
            LoadCommand = new RelayCommand(LoadFromDialog);
            RunCommand = new RelayCommand(Run);
            NewCommand = new RelayCommand(New);
        }

        public RelayCommand CompileCommand { get; private set; }
        public RelayCommand SaveCommand { get; private set; }
        public RelayCommand NewCommand { get; private set; }
        public RelayCommand LoadCommand { get; private set; }
        public RelayCommand RunCommand { get; private set; }
        public TextEditor CodeEditor { get; set; }
        public TextEditor TreeText { get; set; }
        public TextEditor CodegenText { get; set; }
        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        public ObservableCollection<Token> Tokens { get; } = new ObservableCollection<Token>();

        void New()
        {
            if (CodeEditor.IsModified &&
                MessageBox.Show($"File {Filename} dirty, save first?", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                Save();
            CodeEditor.Text = "";
            Filename = "";
            CodeEditor.IsModified = false;
        }

        void Save()
        {
            if (String.IsNullOrEmpty(filename))
            {
                var sfd = new SaveFileDialog();
                //sfd.DefaultExt = ""
                if (sfd.ShowDialog() == true)
                    Filename = sfd.FileName;
            }
            if (CodeEditor.IsModified)
            {
                if (!string.IsNullOrEmpty(filename))
                {
                    File.WriteAllText(filename, CodeEditor.Text);
                    Messages.Add($"File {Filename} saved");
                    CodeEditor.IsModified = false;
                }
            }
        }

        void LoadFromDialog()
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
                Load(ofd.FileName);
        }

        void Load(string filename)
        {
            var text = File.ReadAllText(filename);
            CodeEditor.Text = text;
            Filename = filename;
            CodeEditor.IsModified = false;
            Messages.Add($"File {Filename} loaded");
        }

        Compiler compiler;
        Environment env;

        // compile, check env after to check success
        void Compile()
        {
            try
            {
                Save();

                var output = new StringWriter();
                compiler = new Compiler();
                env = new Environment(output);

                TreeText.Text = "";
                CodegenText.Text = "";
                var sourceCodeText = CodeEditor.Text;


                compiler.Compile(sourceCodeText, env);

                // output messages
                var msgs = output.ToString().Split('\n');
                Messages.Clear();
                foreach (var msg in msgs)
                    Messages.Add(msg.Replace("\r", "").Replace("\n", ""));

                // some output
                TreeText.Text = compiler.SyntaxTreeToText();
                CodegenText.Text = compiler.CodegenToText();
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

        void Run()
        {
            Compile();
            if (env.ErrorCount == 0)
            {
                var r = new Runtime();
                r.Run(env);

                // output messages
                Messages.Clear();
                var msgs = env.Output.ToString().Split('\n');
                Messages.Clear();
                foreach (var msg in msgs)
                    Messages.Add(msg.Replace("\r", "").Replace("\n", ""));
            }
        }

        // editor article at http://www.codeproject.com/Articles/42490/Using-AvalonEdit-WPF-Text-Editor

        string filename = "";
        public string Filename
        {
            get
            {
                return filename;
            }
            set
            {
                Set<string>(() => this.Filename, ref filename, value);
            }
        }

        string modified = "";
        public string Modified
        {
            get
            {
                return modified;
            }
            set
            {
                Set<string>(() => this.Modified, ref modified, value);
            }
        }


        public void Loaded()
        {
            var filename = Properties.Settings.Default.LastFilename;
            if (!String.IsNullOrEmpty(filename) && File.Exists(filename))
                Load(filename);
        }

        public void Closing()
        {
            Save();
            Properties.Settings.Default.LastFilename = Filename;
            Properties.Settings.Default.Save();
        }

    }
}