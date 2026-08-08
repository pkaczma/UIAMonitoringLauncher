using System.Windows;
using System.Windows.Input;
using AutoLib.Models;
using MonitoringLauncherWPF.Core;

namespace MonitoringLauncherWPF.Views
{
    public partial class StepEditWindow : Window
    {
        public AutomationStep Step { get; private set; }
        public bool IsSaved { get; private set; } = false;

        public StepEditWindow(AutomationStep stepToEdit)
        {
            InitializeComponent();

            Logger.Info(this, $"Rozpoczęto edycję kroku (Tryb Głębokiej Kopii): {stepToEdit.ElementName ?? "Brak nazwy"} (ID: {stepToEdit.StepId})");

            Step = new AutomationStep
            {
                StepId = stepToEdit.StepId,
                Type = stepToEdit.Type,
                AppPath = stepToEdit.AppPath,
                Arguments = stepToEdit.Arguments,
                AutomationId = stepToEdit.AutomationId,
                ElementName = stepToEdit.ElementName,
                ControlType = stepToEdit.ControlType,
                WindowX = stepToEdit.WindowX,
                WindowY = stepToEdit.WindowY,
                WindowWidth = stepToEdit.WindowWidth,
                WindowHeight = stepToEdit.WindowHeight,
                WindowClassName = stepToEdit.WindowClassName,
                ProcessName = stepToEdit.ProcessName,
                RequiredWindowContent = stepToEdit.RequiredWindowContent,
                Value = stepToEdit.Value,
                TimeoutMs = stepToEdit.TimeoutMs,
                DelayBeforeMs = stepToEdit.DelayBeforeMs,
                TreePath = stepToEdit.TreePath != null ? new System.Collections.Generic.List<int>(stepToEdit.TreePath) : null
            };

            // Ustawiamy DataContext na to okno, aby mechanizm WPF sam wiedział kiedy chować/pokazywać pola wg. ActionType
            DataContext = this;
            
            LoadDataToUI();
        }

        private void LoadDataToUI()
        {
            TypeTextBox.Text = Step.Type.ToString();
            
            AppPathTextBox.Text = Step.AppPath;
            ArgumentsTextBox.Text = Step.Arguments;
            
            NameTextBox.Text = Step.ElementName;
            IdTextBox.Text = Step.AutomationId;
            ValueTextBox.Text = Step.Value;
            
            ClassTextBox.Text = Step.WindowClassName;
            ProcessTextBox.Text = Step.ProcessName;
            RequiredContentTextBox.Text = Step.RequiredWindowContent;

            DelayTextBox.Text = Step.DelayBeforeMs.ToString();
            TimeoutTextBox.Text = Step.TimeoutMs.ToString();

            WinXTextBox.Text = Step.WindowX?.ToString() ?? "";
            WinYTextBox.Text = Step.WindowY?.ToString() ?? "";
            WinWTextBox.Text = Step.WindowWidth?.ToString() ?? "";
            WinHTextBox.Text = Step.WindowHeight?.ToString() ?? "";
        }

        private void HeaderBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void InsertKeePass_Click(object sender, RoutedEventArgs e)
        {
            if (!KeyPassMgr.IsDBLoaded)
            {
                MessageBox.Show("Baza KeePass nie jest otwarta!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var searchWindow = new KeePassSearchWindow(KeePassFieldType.Password) { Owner = this };
            if (searchWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(searchWindow.SelectedTag))
            {
                ValueTextBox.Text = searchWindow.SelectedTag;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Step.AppPath = AppPathTextBox.Text;
            Step.Arguments = ArgumentsTextBox.Text;
            
            Step.ElementName = NameTextBox.Text;
            Step.AutomationId = IdTextBox.Text;
            Step.Value = ValueTextBox.Text;
            
            Step.WindowClassName = ClassTextBox.Text;
            Step.ProcessName = ProcessTextBox.Text;
            Step.RequiredWindowContent = RequiredContentTextBox.Text;

            if (int.TryParse(DelayTextBox.Text, out int delay)) Step.DelayBeforeMs = delay;
            if (int.TryParse(TimeoutTextBox.Text, out int timeout)) Step.TimeoutMs = timeout;

            if (int.TryParse(WinXTextBox.Text, out int wx)) Step.WindowX = wx; else Step.WindowX = null;
            if (int.TryParse(WinYTextBox.Text, out int wy)) Step.WindowY = wy; else Step.WindowY = null;
            if (int.TryParse(WinWTextBox.Text, out int ww)) Step.WindowWidth = ww; else Step.WindowWidth = null;
            if (int.TryParse(WinHTextBox.Text, out int wh)) Step.WindowHeight = wh; else Step.WindowHeight = null;

            IsSaved = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}