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
using System.Runtime.InteropServices;
using System.Text;

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
                env = new Environment(output);
                compiler = new Compiler(env);
                

                TreeText.Text = "";
                CodegenText.Text = "";
                SymbolText.Text = "";
                var sourceCodeText = CodeEditor.Text;
                compiler.Compile(sourceCodeText);

                // output messages
                var msgs = output.ToString().Split('\n');
                Messages.Clear();
                foreach (var msg in msgs)
                    Messages.Add(msg.Replace("\r", "").Replace("\n", ""));

                // some output
                TreeText.Text = compiler.SyntaxTreeToText();
                CodegenText.Text = compiler.CodegenToText();
                SymbolText.Text = compiler.SymbolTableToText();
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
            TraceText.Text = "";
            Compile();

            if (env.ErrorCount == 0)
            {
                env.Info("Testing bytecode in runtime environment....");

                var traceEnv = new Environment(new StringWriter());
                var r = new Runtime(traceEnv);
                var importHandler = new Imports();
                r.HandleImport = importHandler.HandleImport;

                // get parameters
                var words = RunParameters.Split(new[] {" "}, StringSplitOptions.RemoveEmptyEntries);
                var parameters = new int[words.Length];
                for (var i = 0; i < words.Length; ++i)
                {
                    int val;
                    if (Int32.TryParse(words[i], out val))
                        parameters[i] = val;
                    else
                        parameters[i] = 0;
                }

                var returnValues = new int[RunReturnValues];
                var success = r.Run(compiler.CompiledAssembly, RunEntryAttribute, parameters, returnValues);

                if (success)
                {
                    env.Info(" .... runtime successful.");
                    var sb = new StringBuilder();
                    for (var i = 0; i < returnValues.Length; ++i)
                        sb.Append($"{returnValues[i]} ");
                    env.Info($"Return values: {sb}");
                }
                else
                {
                    env.Info("  .... runtime failed. See Trace.");
                }

                // output trace
                TraceText.Text = traceEnv.Output.ToString();
            }

            // output messages
            Messages.Clear();
            var msgs = env.Output.ToString().Split('\n');
            Messages.Clear();
            foreach (var msg in msgs)
                Messages.Add(msg.Replace("\r", "").Replace("\n", ""));

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

        #region Shown panels

        bool showCode = true;
        public bool ShowCode
        {
            get
            {
                return showCode;
            }
            set
            {
                Set(() => this.ShowCode, ref showCode, value);
            }
        }

        bool showAst = true;
        public bool ShowAst
        {
            get
            {
                return showAst;
            }
            set
            {
                Set(() => this.ShowAst, ref showAst, value);
            }
        }
        bool showSymbols = true;
        public bool ShowSymbols
        {
            get
            {
                return showSymbols;
            }
            set
            {
                Set(() => this.ShowSymbols, ref showSymbols, value);
            }
        }

        bool showCodegen = true;
        public bool ShowCodegen
        {
            get
            {
                return showCodegen;
            }
            set
            {
                Set(() => this.ShowCodegen, ref showCodegen, value);
            }
        }
        bool showLexer = true;
        public bool ShowLexer
        {
            get
            {
                return showLexer;
            }
            set
            {
                Set(() => this.ShowLexer, ref showLexer, value);
            }
        }
        bool showtrace = true;
        public bool ShowTrace
        {
            get
            {
                return showtrace;
            }
            set
            {
                Set(() => this.ShowTrace, ref showtrace, value);
            }
        }

        string runEntryAttribute = "Entry";
        public string RunEntryAttribute
        {
            get
            {
                return runEntryAttribute;
            }
            set
            {
                Set(() => this.RunEntryAttribute, ref runEntryAttribute, value);
            }
        }

        int runReturnValues = 1;
        public int RunReturnValues
        {
            get
            {
                return runReturnValues;
            }
            set
            {
                Set(() => this.RunReturnValues, ref runReturnValues, value);
            }
        }

        string runParameters = "";
        public string RunParameters
        {
            get
            {
                return runParameters;
            }
            set
            {
                Set(() => this.RunParameters, ref runParameters, value);
            }
        }

        #endregion


        public TextEditor SymbolText { get; set; }
        public TextEditor TraceText { get; set; }


        public void Loaded()
        {
            var filename = Properties.Settings.Default.LastFilename;
            if (!String.IsNullOrEmpty(filename) && File.Exists(filename))
                Load(filename);

            RunEntryAttribute = Properties.Settings.Default.RunEntryAttribute;
            RunReturnValues = Properties.Settings.Default.RunNumReturnValues;
            RunParameters = Properties.Settings.Default.RunParameters;


            var v = Properties.Settings.Default.OpenViews;
            ShowCode    = (v &  1) != 0;
            ShowAst     = (v &  2) != 0;
            ShowSymbols = (v &  4) != 0;
            ShowCodegen = (v &  8) != 0;
            ShowLexer   = (v & 16) != 0;
            ShowTrace   = (v & 32) != 0;
        }

        public void Closing()
        {
            Save();
            Properties.Settings.Default.LastFilename = Filename;
            Properties.Settings.Default.RunEntryAttribute = RunEntryAttribute;
            Properties.Settings.Default.RunNumReturnValues = RunReturnValues;
            Properties.Settings.Default.RunParameters = RunParameters;
            var v =
                (!ShowCode    ? 0 :  1) +
                (!ShowAst     ? 0 :  2) +
                (!ShowSymbols ? 0 :  4) +
                (!ShowCodegen ? 0 :  8) +
                (!ShowLexer   ? 0 : 16) + 
                (!ShowTrace   ? 0 : 32);
            Properties.Settings.Default.OpenViews = v;
            Properties.Settings.Default.Save();
        }

    }
}