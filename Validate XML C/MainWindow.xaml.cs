using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Xml.Schema;
using System.Xml;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Validate_XML
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool cancelThread = false;
        private int errorLine = -1;

        /// <summary>
        /// Default Constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            this.Icon = Imaging.CreateBitmapSourceFromHBitmap(Properties.Resources.xml_validate.ToBitmap().GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            this.chkMultipleErrors.IsChecked = Properties.Settings.Default.ShowMultipleErrors;

            if (File.Exists(Properties.Settings.Default.XmlFile))
            {
                this.txtBrowseXml.Text = Properties.Settings.Default.XmlFile;
            }

            if (File.Exists(Properties.Settings.Default.XsdFile))
            {
                this.txtBrowseXsd.Text = Properties.Settings.Default.XsdFile;
            }
        }

        /// <summary>
        /// Event that fires when the window is closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.ShowMultipleErrors = this.chkMultipleErrors.IsChecked.Value;
            Properties.Settings.Default.XmlFile = this.txtBrowseXml.Text;
            Properties.Settings.Default.XsdFile = this.txtBrowseXsd.Text;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// This is the event that fires to validate the XML file against the xsd file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks></remarks>
        private void btnValidate_Click(System.Object sender, System.Windows.RoutedEventArgs e)
        {
            grdLoading.Visibility = System.Windows.Visibility.Visible;
            this.chkMultipleErrors.IsEnabled = false;
            this.txtResults.IsEnabled = false;
            this.txtBrowseXml.IsEnabled = false;
            this.txtBrowseXsd.IsEnabled = false;
            this.btnBrowseXml.IsEnabled = false;
            this.btnBrowseXsd.IsEnabled = false;
            this.btnValidate.IsEnabled = false;
            this.btnCancel.IsEnabled = true;

            Thread thread = new Thread(new ParameterizedThreadStart(Validate));
            thread.Start(new object[] {
			    this.txtBrowseXsd.Text,
			    this.txtBrowseXml.Text,
                this.chkMultipleErrors.IsChecked.Value
		    });
        }

        private void Validate(object obj)
        {
            //clear the box and start over
            WriteLine(" **************** Validation Started **************** " + Environment.NewLine + Environment.NewLine, true);

            XmlSchemaSet xmlSchema = new XmlSchemaSet();
            xmlSchema.Add(null, (string)((object[])obj)[0]);

            //create settings and add the schema to it
            XmlReaderSettings xmlSettings = new XmlReaderSettings();
            xmlSettings.Schemas = xmlSchema;
            xmlSettings.ValidationType = ValidationType.Schema;

            bool stopValidating = false;

            //now create the xmlreader with the settings from above
            XmlReader xml = XmlReader.Create((string)((object[])obj)[1], xmlSettings);
            if ((bool)((object[])obj)[2])
            {
                while (!xml.EOF && !stopValidating && !cancelThread)
                {
                    DoValidation(xml, out stopValidating);
                }
                cancelThread = false;
            }
            else
            {
                DoValidation(xml);
            }

            xml.Close();

            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                new System.Windows.Threading.DispatcherOperationCallback(delegate
                {
                    grdLoading.Visibility = System.Windows.Visibility.Collapsed;
                    this.txtBrowseXml.IsEnabled = true;
                    this.txtBrowseXsd.IsEnabled = true;
                    this.btnBrowseXml.IsEnabled = true;
                    this.btnBrowseXsd.IsEnabled = true;
                    this.btnValidate.IsEnabled = true;
                    this.chkMultipleErrors.IsEnabled = true;
                    this.txtResults.IsEnabled = true;
                    return null;
                }), null);
        }

        /// <summary>
        /// Does the validation overload
        /// </summary>
        /// <param name="count"></param>
        /// <param name="xml"></param>
        /// <returns></returns>
        private void DoValidation(XmlReader xml)
        {
            bool stopValidating = false;
            DoValidation(xml, out stopValidating);
        }

        /// <summary>
        /// Does the xml validation
        /// </summary>
        /// <param name="count"></param>
        /// <param name="xml"></param>
        /// <param name="lastErrorCount"></param>
        /// <param name="lastErrorCountOutput"></param>
        /// <param name="stopValidating"></param>
        /// <returns></returns>
        private void DoValidation(XmlReader xml, out bool stopValidating)
        {
            stopValidating = false;
            try
            {
                while (xml.Read())
                {
                    if (xml.Depth == 0 && xml.IsStartElement())
                    {
                        WriteLine(xml.Name + " ... Passed" + Environment.NewLine, false);
                    }
                }
                WriteLine(Environment.NewLine + " *************** Validation Completed *************** ", false);
            }
            catch (Exception ex)
            {
                if (!ex.Message.Equals("The transition from the 'ValidateElement' method to the 'ValidateText' method is not allowed.") &&
                    !ex.Message.Equals("The transition from the 'ValidateElement' method to the 'ValidateWhitespace' method is not allowed.") &&
                    !ex.Message.Equals("The transition from the 'ValidateElement' method to the 'ValidateElement' method is not allowed.") &&
                    !ex.Message.Equals("The call to the 'ValidateEndElement' method does not match a corresponding call to 'ValidateElement' method.") &&
                    (errorLine + 1) != ((IXmlLineInfo)xml).LineNumber)
                {
                    //if we run across this error message then we're going to try to help the user interpret what it means
                    string errorMessage = Environment.NewLine + "*---------------------------- Error ----------------------------*";
                    if (ex.Message.Contains("List of possible elements expected:"))
                    {
                        errorMessage += Environment.NewLine + "Tag <";
                        string tempError = ex.Message.Remove(0, ex.Message.IndexOf("List of possible elements expected:") + 35);
                        errorMessage += tempError.Replace(" ", string.Empty).Replace("'", string.Empty).Replace(".", string.Empty);
                        errorMessage += "> is missing after line " + ((IXmlLineInfo)xml).LineNumber.ToString();
                    }
                    
                    WriteLine(errorMessage + Environment.NewLine + Environment.NewLine, false);
                    WriteLine(ex.Message + Environment.NewLine + "Line: " + ((IXmlLineInfo)xml).LineNumber.ToString() + Environment.NewLine + Environment.NewLine, false);
                }
                errorLine = ((IXmlLineInfo)xml).LineNumber;

                bool stop = false;
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                    new System.Windows.Threading.DispatcherOperationCallback(delegate
                    {
                        this.txtResults.ScrollToEnd();
                        if (this.txtResults.GetLastVisibleLineIndex() > 1000)
                        {
                            stop = true;
                            this.txtResults.Text += "************************** Max Number of Errors Reached!!!!! **************************";
                        }
                        return null;
                    }), null);
                stopValidating = stop;
            }
            finally
            {

            }
        }

        /// <summary>
        /// Writes a line of text to the text box from a thread
        /// </summary>
        /// <param name="text"></param>
        /// <param name="clear"></param>
        void WriteLine(string text, bool clear)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                new System.Windows.Threading.DispatcherOperationCallback(delegate
                {
                    if (clear)
                    {
                        this.txtResults.Text = text;
                    }
                    else
                    {
                        this.txtResults.Text += text;
                    }
                    return null;
                }), null);
        }

        /// <summary>
        /// This is the method that loads the file dialog and allows the user to select the file to be used for the xml
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks></remarks>
        private void btnBrowseXml_Click(System.Object sender, System.Windows.RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.DefaultExt = ".xml";
            openFileDialog.Filter = "XML Documents (.xml)|*.xml";
            openFileDialog.Multiselect = false;

            if (!string.IsNullOrEmpty(this.txtBrowseXml.Text))
            {
                openFileDialog.InitialDirectory = this.txtBrowseXml.Text;
            }

            if (openFileDialog.ShowDialog() == true)
            {
                this.txtBrowseXml.Text = openFileDialog.FileName;
            }
        }

        /// <summary>
        /// This is the method that loads the file dialog and allows the user to select the file to be used for the xsd
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks></remarks>
        private void btnBrowseXsd_Click(System.Object sender, System.Windows.RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.DefaultExt = ".xsd";
            openFileDialog.Filter = "XSD Documents (.xsd)|*.xsd";
            openFileDialog.Multiselect = false;

            if (!string.IsNullOrEmpty(this.txtBrowseXsd.Text))
            {
                openFileDialog.InitialDirectory = this.txtBrowseXsd.Text;
            }

            if (openFileDialog.ShowDialog() == true)
            {
                this.txtBrowseXsd.Text = openFileDialog.FileName;
            }
        }

        /// <summary>
        /// This method will enable/disable the validate button depending on if there's a file in both xml and xsd text boxes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks></remarks>
        private void txtBrowseXsd_TextChanged(System.Object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (File.Exists(this.txtBrowseXsd.Text) && File.Exists(this.txtBrowseXml.Text))
            {
                this.btnValidate.IsEnabled = true;
            }
            else
            {
                this.btnValidate.IsEnabled = false;
            }
        }

        /// <summary>
        /// This method just calls the browse xsd method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks></remarks>
        private void txtBrowseXml_TextChanged(System.Object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            txtBrowseXsd_TextChanged(null, null);
        }

        /// <summary>
        /// Event that fires when the cancel button is clicked that will cancel the thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            cancelThread = true;
            this.btnCancel.IsEnabled = false;
        }

        /// <summary>
        /// Event that fires when something is drug into a window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetType() == typeof(DataObject) && ((DataObject)e.Data).ContainsFileDropList() &&
                (((DataObject)e.Data).GetFileDropList()[0].ToLower().EndsWith("xml") || (((DataObject)e.Data).GetFileDropList()[0].ToLower().EndsWith("xsd"))))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        /// <summary>
        /// Event that fires when something is dropped into a window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetType() == typeof(DataObject) && ((DataObject)e.Data).ContainsFileDropList())
            {
                foreach (string file in ((DataObject)e.Data).GetFileDropList())
                {
                    if (file.ToLower().EndsWith("xml"))
                    {
                        this.txtBrowseXml.Text = file;
                    }
                    else if (file.ToLower().EndsWith("xsd"))
                    {
                        this.txtBrowseXsd.Text = file;
                    }
                }
            }
        }

        /// <summary>
        /// Event that fires when you drag and leave the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
        }

        /// <summary>
        /// Event to handle dragging in a text control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtControl_PreviewDragEnter(object sender, DragEventArgs e)
        {
            Window_DragEnter(sender, e);
            e.Handled = true;
        }

        /// <summary>
        /// Event to handle dragging across a text control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtControl_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
    }
}
