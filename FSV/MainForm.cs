using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NationalInstruments.VisaNS;
using NationalInstruments.NI4882;
using Telerik.WinControls;
using System.Threading;
using Telerik.WinControls.UI;
using Telerik.Charting;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO;
using System.Diagnostics;
using System.Configuration;
//using Arction.WinForms.Charting.Views.ViewXY;
//using Arction.WinForms.Charting.Axes;
//using Arction.WinForms.Charting.SeriesXY;
//using Arction.WinForms.Charting;
using ZedGraph;

namespace FSV
{


    public partial class MainForm : Telerik.WinControls.UI.RadForm
    {
        GpibSession session;
        Thread measurement;
        int rec_count = 0;
        Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);

        GraphPane exampleGraphPane;
        LineItem exampleLineItem;
        PointPairList examplePointPairLitst;

        public static  string today_filename = DateTime.Now.ToShortDateString();


        public MainForm()
        {
            InitializeComponent();
        }

        private void license_check()
        {

            // 정품인증
            //string license_check = NubicomLicenseManager.NubicomLicenseManager.Check_License("ETC");
            string license_check = "PASS|0|10000";
            string[] result = license_check.Split('|');
            if (result[0] != "PASS")
            {
                MessageBox.Show("정품인증 : " + result[1], "오류");
                if (measurement != null)
                {
                    if (measurement.ThreadState == System.Threading.ThreadState.Running)
                    {
                        measurement.Abort();
                    }
                }
                if (session != null)
                {
                    session.Dispose();
                }
                Application.ExitThread();
                Environment.Exit(0);
            }
            else
            {
                int remain_date = Convert.ToInt32(result[2]);

                if (remain_date < 7)
                {
                    MessageBox.Show("라이센스 유효기간이 " + remain_date.ToString() + "일 남았습니다.\n(주)누비콤으로 문의하여 주시기 바랍니다.", "알림");
                }
            }

        }

        private void MainForm_Load(object sender, EventArgs e)
        {


            license_check();

            disable_components();
            PrepareProcess();

            ////Initialize chart
            //lightningChartUltimate1.BeginUpdate();
            ////Get XY view
            //ViewXY chartView = lightningChartUltimate1.ViewXY;
            ////Get default x-axis and set the range and ValueType
            //AxisX axisX = chartView.XAxes[0];
            //axisX.SetRange(0, 690);
            //axisX.ValueType = AxisValueType.Number;
            ////Get default y-axis and set the range.
            //AxisY axisY = chartView.YAxes[0];
            //axisY.SetRange(-120, -40);
            //chartView.PointLineSeries.Clear();
            //lightningChartUltimate1.EndUpdate();


            comboBox_freq.SelectedIndex = 0;
            comboBox_freq_unit.SelectedIndex = 1;
            comboBox_span.SelectedIndex = 2;
            comboBox_rbw.SelectedIndex = 3;
            comboBox_vbw.SelectedIndex = 3;

            radButton_Start.Enabled = true;
            radButton_Stop.Enabled = false;

            radPanel_rec.BackColor = Color.DarkGreen;
            radPanel_rx.BackColor = Color.DarkRed;
            radPanel_tx.BackColor = Color.DarkBlue;

            chart_peak.ChartAreas[0].AxisX.ScrollBar.Enabled = true;
            chart_peak.ChartAreas[0].AxisX.IsLabelAutoFit = true;
            chart_peak.ChartAreas[0].AxisX.ScaleView.Size = 40;
            chart_peak.ChartAreas[0].AxisX.Interval = 10.0;
            chart_peak.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Seconds;
            chart_peak.ChartAreas[0].AxisX.LabelStyle.Format = "HH:mm:ss";
            chart_peak.Series[0].XValueType = ChartValueType.Time;
            chart_peak.ChartAreas[0].AxisX.IntervalAutoMode = IntervalAutoMode.FixedCount;

            chart_peak.GetToolTipText += this.chart_peak_GetToolTipText;
            chart_trace.GetToolTipText += this.chart_trace_GetToolTipText;

            string sDirPath;
            sDirPath = Application.StartupPath + ".\\capture";
            DirectoryInfo di = new DirectoryInfo(sDirPath);
            if (di.Exists == false)
            {
                di.Create();
            }

            
            sDirPath = Application.StartupPath + ".\\data";
            di = new DirectoryInfo(sDirPath);
            if (di.Exists == false)
            {
                di.Create();
            }

            check_config();

            radButton_refresh_Click(this, new EventArgs());


            // Database Connect
            Database.Connect();

            check_limit();
        }

        private void chart_peak_GetToolTipText(object sender, ToolTipEventArgs e)
        {
            // Check selected chart element and set tooltip text for it
            switch (e.HitTestResult.ChartElementType)
            {
                case ChartElementType.DataPoint:
                    var dataPoint = e.HitTestResult.Series.Points[e.HitTestResult.PointIndex];
                    e.Text = string.Format("X:\t{0}\nY:\t{1}", dataPoint.XValue, dataPoint.YValues[0]);
                    break;
            }
        }

        private void chart_trace_GetToolTipText(object sender, ToolTipEventArgs e)
        {
            // Check selected chart element and set tooltip text for it
            switch (e.HitTestResult.ChartElementType)
            {
                case ChartElementType.DataPoint:
                    var dataPoint = e.HitTestResult.Series.Points[e.HitTestResult.PointIndex];
                    e.Text = string.Format("X:\t{0}\nY:\t{1}", dataPoint.XValue, dataPoint.YValues[0]);
                    break;
            }
        }
        private void radButton_refresh_Click(object sender, EventArgs e)
        {
            try
            {
                comboBox_device.Items.Clear();
                string[] resources = ResourceManager.GetLocalManager().FindResources("?*");
                if (resources.Length == 0)
                {
                    Telerik.WinControls.RadMessageBox.Show("장비를 찾을 수 없습니다!", "경고");
                }
                else
                {
                    for (int i = 0; i < resources.Length; i++)
                    {
                        if (LeftStr(resources[i], 4) == "GPIB" && RightStr(resources[i], 5) == "INSTR")
                        {
                            comboBox_device.Items.Add(resources[i]);
                        }
                    }
                    if (comboBox_device.Items.Count > 0)
                    {
                        comboBox_device.SelectedIndex = 0;
                        radButton_Start.Enabled = true;
                    }
                    if (comboBox_device.Items.Count == 1)
                    {
                        radButton_connect_Click(this, new EventArgs());
                    }
                }
            }
            catch (Exception ex)
            {
                RadMessageBox.Show(ex.Message);
            }
        }


        private void radButton_connect_Click(object sender, EventArgs e)
        {
            if (radButton_connect.Text == "Connect")
            {
                string CONNECT_STR = comboBox_device.Text;
                session = new GpibSession(CONNECT_STR);
                string result = session.Query("*IDN?");
                label_device_name.Text = result;
                radButton_connect.Text = "Disconnect";
                enable_components();
                radButton_get_setting_Click(this, new EventArgs());
            }
            else
            {
                session.Clear();
                session.Dispose();
                radButton_connect.Text = "Connect";
                disable_components();
            }
        }

        private void enable_components()
        {
            comboBox_freq.Enabled = true;
            comboBox_freq_unit.Enabled = true;
            radSpinEditor_tx.Enabled = true;
            radSpinEditor_rx.Enabled = true;

            radSpinEditor_span.Enabled = true;
            comboBox_span.Enabled = true;
            radSpinEditor_tx_limit.Enabled = true;
            radSpinEditor_rx_limit.Enabled = true;

            radSpinEditor_rbw.Enabled = true;
            comboBox_rbw.Enabled = true;
            radSpinEditor_vbw.Enabled = true;
            comboBox_vbw.Enabled = true;
            
            radButton_get_setting.Enabled = true;
            radButton_setup.Enabled = true;
        }

        private void disable_components()
        {
            comboBox_freq.Enabled = false;
            comboBox_freq_unit.Enabled = false;
            radSpinEditor_tx.Enabled = false;
            radSpinEditor_rx.Enabled = false;

            radSpinEditor_span.Enabled = false;
            comboBox_span.Enabled = false;
            radSpinEditor_tx_limit.Enabled = false;
            radSpinEditor_rx_limit.Enabled = false;

            radSpinEditor_rbw.Enabled = false;
            comboBox_rbw.Enabled = false;
            radSpinEditor_vbw.Enabled = false;
            comboBox_vbw.Enabled = false;
            
            radButton_get_setting.Enabled = false;
            radButton_setup.Enabled = false;
        }

        public static string LeftStr(string param, int length)
        {
            //we start at 0 since we want to get the characters starting from the
            //left and with the specified lenght and assign it to a variable
            string result = param.Substring(0, length);
            //return the result of the operation
            return result;
        }
        public static string RightStr(string param, int length)
        {
            //start at the index based on the lenght of the sting minus
            //the specified lenght and assign it a variable
            string result = param.Substring(param.Length - length, length);
            //return the result of the operation
            return result;
        }
        public static string MidStr(string param, int startIndex, int length)
        {
            //start at the specified index in the string ang get N number of
            //characters depending on the lenght and assign it to a variable
            string result = param.Substring(startIndex, length);
            //return the result of the operation
            return result;
        }
        public static string MidStr(string param, int startIndex)
        {
            //start at the specified index and return all characters after it
            //and assign it to a variable
            string result = param.Substring(startIndex);
            //return the result of the operation
            return result;
        }

        private void check_config()
        {
            double value;
            if (config.AppSettings.Settings["freq"] == null)
            {
                value = convert_double(Convert.ToDecimal(comboBox_freq.Text), comboBox_freq_unit.Text);
                config.AppSettings.Settings.Add("freq", value.ToString());
            }

            if (config.AppSettings.Settings["span"] == null)
            {
                value = convert_double(radSpinEditor_span.Value, comboBox_span.Text);
                config.AppSettings.Settings.Add("span", value.ToString());
            }

            if (config.AppSettings.Settings["rbw"] == null)
            {
                value = convert_double(radSpinEditor_rbw.Value, comboBox_rbw.Text);
                config.AppSettings.Settings.Add("rbw", value.ToString());
            }

            if (config.AppSettings.Settings["vbw"] == null)
            {
                value = convert_double(radSpinEditor_vbw.Value, comboBox_vbw.Text);
                config.AppSettings.Settings.Add("vbw", value.ToString());
            }

            if (config.AppSettings.Settings["tx_level"] == null)
            {
                value = Convert.ToDouble(radSpinEditor_tx.Value);
                config.AppSettings.Settings.Add("tx_level", value.ToString());
            }

            if (config.AppSettings.Settings["rx_level"] == null)
            {
                value = Convert.ToDouble(radSpinEditor_rx.Value);
                config.AppSettings.Settings.Add("rx_level", value.ToString());
            }


            if (config.AppSettings.Settings["tx_limit"] == null)
            {
                value = Convert.ToDouble(radSpinEditor_tx_limit.Value);
                config.AppSettings.Settings.Add("tx_limit", value.ToString());
            }

            if (config.AppSettings.Settings["rx_limit"] == null)
            {
                value = Convert.ToDouble(radSpinEditor_rx_limit.Value);
                config.AppSettings.Settings.Add("rx_limit", value.ToString());
            }

            if (config.AppSettings.Settings["rec_limit"] == null)
            {
                value = Convert.ToDouble(radSpinEditor_rec_limit.Value);
                config.AppSettings.Settings.Add("rec_limit", value.ToString());
            }
            config.Save(ConfigurationSaveMode.Minimal);
        }


        private void set_config()
        {
            double value;
            value = convert_double(Convert.ToDecimal(comboBox_freq.Text), comboBox_freq_unit.Text);
            config.AppSettings.Settings.Remove("freq");
            config.AppSettings.Settings.Add("freq", value.ToString());

            value = convert_double(radSpinEditor_span.Value, comboBox_span.Text);
            config.AppSettings.Settings.Remove("span");
            config.AppSettings.Settings.Add("span", value.ToString());

            value = convert_double(radSpinEditor_rbw.Value, comboBox_rbw.Text);
            config.AppSettings.Settings.Remove("rbw");
            config.AppSettings.Settings.Add("rbw", value.ToString());

            value = convert_double(radSpinEditor_vbw.Value, comboBox_vbw.Text);
            config.AppSettings.Settings.Remove("vbw");
            config.AppSettings.Settings.Add("vbw", value.ToString());

            value = Convert.ToDouble(radSpinEditor_tx.Value);
            config.AppSettings.Settings.Remove("tx_level");
            config.AppSettings.Settings.Add("tx_level", value.ToString());

            value = Convert.ToDouble(radSpinEditor_rx.Value);
            config.AppSettings.Settings.Remove("rx_level");
            config.AppSettings.Settings.Add("rx_level", value.ToString());

            value = Convert.ToDouble(radSpinEditor_tx_limit.Value);
            config.AppSettings.Settings.Remove("tx_limit");
            config.AppSettings.Settings.Add("tx_limit", value.ToString());

            value = Convert.ToDouble(radSpinEditor_rx_limit.Value);
            config.AppSettings.Settings.Remove("rx_limit");
            config.AppSettings.Settings.Add("rx_limit", value.ToString());

            value = Convert.ToDouble(radSpinEditor_rec_limit.Value);
            config.AppSettings.Settings.Remove("rec_limit");
            config.AppSettings.Settings.Add("rec_limit", value.ToString());

            config.Save(ConfigurationSaveMode.Minimal);
        }

        private void radButton_get_setting_Click(object sender, EventArgs e)
        {
            string result;
            data_convert con;


            //config.AppSettings.Settings.Remove("Span");
            //config.AppSettings.Settings.Add("Span", "40MHz");
            //string span = config.AppSettings.Settings["Span"].Value;
            //string freq = config.AppSettings.Settings["freq"].Value;
            //config.Save(ConfigurationSaveMode.Minimal);
            //MessageBox.Show(span);


            //result = session.Query("SENSe:FREQuency:CENTer?");
            result = config.AppSettings.Settings["freq"].Value;
            con = convert(result);
            decimal freq = Convert.ToDecimal(con.data);
            for (int i = 0; i < comboBox_freq.Items.Count; i++)
            {
                if (Convert.ToDecimal(comboBox_freq.Items[i]) == freq)
                {
                    comboBox_freq.SelectedIndex = i;
                    break;
                }
            }
            comboBox_freq_unit.SelectedIndex = con.selectedindex;


            //result = session.Query("SENSe:FREQuency:SPAN?");
            result = config.AppSettings.Settings["span"].Value;
            con = convert(result);
            radSpinEditor_span.Value = Convert.ToDecimal(con.data);
            comboBox_span.SelectedIndex = con.selectedindex;



            //result = session.Query("BAND:RES?");
            result = config.AppSettings.Settings["rbw"].Value;
            con = convert(result);
            radSpinEditor_rbw.Value = Convert.ToDecimal(con.data);
            comboBox_rbw.SelectedIndex = con.selectedindex;


            //result = session.Query("BAND:VIDEO?");
            result = config.AppSettings.Settings["vbw"].Value;
            con = convert(result);
            radSpinEditor_vbw.Value = Convert.ToDecimal(con.data);
            comboBox_vbw.SelectedIndex = con.selectedindex;

            double result_f;
            
            result = config.AppSettings.Settings["tx_level"].Value;
            result_f = Convert.ToDouble(result);
            radSpinEditor_tx.Value = Convert.ToDecimal(result_f);
            
            result = config.AppSettings.Settings["rx_level"].Value;
            result_f = Convert.ToDouble(result);
            radSpinEditor_rx.Value = Convert.ToDecimal(result_f);

            result = config.AppSettings.Settings["tx_limit"].Value;
            result_f = Convert.ToDouble(result);
            radSpinEditor_tx_limit.Value = Convert.ToDecimal(result_f);

            result = config.AppSettings.Settings["rx_limit"].Value;
            result_f = Convert.ToDouble(result);
            radSpinEditor_rx_limit.Value = Convert.ToDecimal(result_f);


            result = config.AppSettings.Settings["rec_limit"].Value;
            result_f = Convert.ToDouble(result);
            radSpinEditor_rec_limit.Value = Convert.ToDecimal(result_f);

        }

        public class data_convert
        {
            public double data;
            public string unit;
            public int selectedindex;
        }

        private data_convert convert(string data)
        {
            data_convert con = new data_convert();
            double data_f = Convert.ToDouble(data);
            if (data_f >= 1000000000.0)
            {
                data_f = data_f / 1000000000.0;
                con.data = data_f;
                con.unit = "GHz";
                con.selectedindex = 0;
            }
            else if (data_f >= 1000000.0)
            {
                data_f = data_f / 1000000.0;
                con.data = data_f;
                con.unit = "MHz";
                con.selectedindex = 1;
            }
            else if (data_f >= 1000.0)
            {
                data_f = data_f / 1000.0;
                con.data = data_f;
                con.unit = "KHz";
                con.selectedindex = 2;
            }
            else
            {
                con.data = data_f;
                con.unit = "Hz";
                con.selectedindex = 3;

            }

            return con;
        }


        private data_convert convert(decimal data)
        {
            data_convert con = new data_convert();
            double data_f = Convert.ToDouble(data);
            if (data_f >= 1000000000.0)
            {
                data_f = data_f / 1000000000.0;
                con.data = data_f;
                con.unit = "GHz";
                con.selectedindex = 0;
            }
            else if (data_f >= 1000000.0)
            {
                data_f = data_f / 1000000.0;
                con.data = data_f;
                con.unit = "MHz";
                con.selectedindex = 1;
            }
            else if (data_f >= 1000.0)
            {
                data_f = data_f / 1000.0;
                con.data = data_f;
                con.unit = "KHz";
                con.selectedindex = 2;
            }
            else
            {
                con.data = data_f;
                con.unit = "Hz";
                con.selectedindex = 3;

            }

            return con;
        }


        private data_convert convert(double data)
        {
            data_convert con = new data_convert();
            double data_f = Convert.ToDouble(data);
            if (data_f >= 1000000000.0)
            {
                data_f = data_f / 1000000000.0;
                con.data = data_f;
                con.unit = "GHz";
                con.selectedindex = 0;
            }
            else if (data_f >= 1000000.0)
            {
                data_f = data_f / 1000000.0;
                con.data = data_f;
                con.unit = "MHz";
                con.selectedindex = 1;
            }
            else if (data_f >= 1000.0)
            {
                data_f = data_f / 1000.0;
                con.data = data_f;
                con.unit = "KHz";
                con.selectedindex = 2;
            }
            else
            {
                con.data = data_f;
                con.unit = "Hz";
                con.selectedindex = 3;

            }

            return con;
        }

        private void radButton_setup_Click(object sender, EventArgs e)
        {
            license_check();

            string command;

            command = "SENSe:FREQuency:CENTer " + comboBox_freq.Text + comboBox_freq_unit.Text;
            session.Write(command);

            command = "SENSe:FREQuency:SPAN " + radSpinEditor_span.Value.ToString() + comboBox_span.Text;
            session.Write(command);

            command = "BAND:RES " + radSpinEditor_rbw.Value.ToString() + comboBox_rbw.Text;
            session.Write(command);

            command = "BAND:VIDEO " + radSpinEditor_vbw.Value.ToString() + comboBox_vbw.Text;
            session.Write(command);
            
        }


        private double convert_double(decimal value,string unit)
        {
            double result = 0.0;
            if (unit.ToUpper() == "GHZ")
            {
                result = Convert.ToDouble(value) * 1000000000.0;
            }
            else if (unit.ToUpper() == "MHZ")
            {
                result = Convert.ToDouble(value) * 1000000.0;
            }
            else if (unit.ToUpper() == "KHZ")
            {
                result = Convert.ToDouble(value) * 1000.0;
            }
            else
            {
                result = Convert.ToDouble(value);
            }
            return result;
        }

        private void radButton_measurement_Click(object sender, EventArgs e)
        {
            //double center = convert_double(radSpinEditor_Freq.Value, comboBox_freq.Text);
            //double span = convert_double(radSpinEditor_span.Value, comboBox_span.Text);
            //double max = center + span;
            //double min = center - span;

            //data_convert maxcon = convert(max);
            //data_convert mincon = convert(min);

            //chart_trace.ChartAreas[0].Axes[0].Maximum = maxcon.data;
            //chart_trace.ChartAreas[0].Axes[0].Minimum = mincon.data;
            //chart_trace.ChartAreas[0].Axes[0].LabelStyle.Format = "{0.000:0} " + maxcon.unit;

            measurement = new Thread(fetch);
            measurement.Start();
            radButton_Start.Enabled = false;
            radButton_Stop.Enabled = true;
            radButton_setup.Enabled = false;
            radButton_get_setting.Enabled = false;
            
        }

        private void fetch()
        {
            //decimal start = 0;
            //decimal stop = 0;
            //decimal interval = 0;
            try
            {
                Invoke((MethodInvoker)delegate
                {
                    chart_trace.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
                    chart_trace.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
                    chart_trace.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

                    chart_peak.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
                    chart_peak.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
                    chart_peak.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

                    decimal freq = Convert.ToDecimal(comboBox_freq.Text);
                    if (comboBox_freq_unit.SelectedIndex == 0)
                    {
                        freq *= 1000000000;
                    }
                    else if (comboBox_freq_unit.SelectedIndex == 1)
                    {
                        freq *= 1000000;
                    }
                    else if (comboBox_freq_unit.SelectedIndex == 2)
                    {
                        freq *= 1000;
                    }
                    else if (comboBox_freq_unit.SelectedIndex == 3)
                    {
                        freq *= 1;
                    }


                    decimal span = radSpinEditor_span.Value;
                    if (comboBox_span.SelectedIndex == 0)
                    {
                        span *= 1000000000;
                    }
                    else if (comboBox_span.SelectedIndex == 1)
                    {
                        span *= 1000000;
                    }
                    else if (comboBox_span.SelectedIndex == 2)
                    {
                        span *= 1000;
                    }
                    else if (comboBox_span.SelectedIndex == 3)
                    {
                        span *= 1;
                    }

                    //start = freq - span;
                    //stop = freq + span;
                    //interval = (stop - start) / (radSpinEditor_point.Value - 1);

                    //data_convert con_start = convert(start);
                    //data_convert con_stop = convert(stop);
                    //차트의 X축 최소값과 최대값, Y축 최소값과 최대값
                    //chart1.ChartAreas[0].AxisX.Minimum = Convert.ToDouble(start);
                    //chart1.ChartAreas[0].AxisX.Maximum = Convert.ToDouble(stop);
                    //chart1.ChartAreas[0].AxisX.Interval = (con_stop.data - con_start.data) / Convert.ToDouble(radSpinEditor_point.Value);
                    //chart1.ChartAreas[0].AxisY.Minimum = -150;
                    //chart1.ChartAreas[0].AxisY.Maximum = 0;


                    //LassoZoomController lassoZoomController = new LassoZoomController();
                    //radChartView_peak.Controllers.Add(lassoZoomController);

                    //ChartPanZoomController panZoomController = new ChartPanZoomController();
                    //panZoomController.PanZoomMode = ChartPanZoomMode.Horizontal;
                    //radChartView_peak.Controllers.Add(panZoomController);
                    


                });

                string command = "INIT:CONT ON";
                session.Write(command);
                command = "FORM REAL,32";   // 바이트로 데이터를 받는다
                //command = "FORM ASCII";   // 콤마 구분자로 데이터를 받는다
                session.Write(command);
                
                Stopwatch sw = new Stopwatch();
                sw.Start();

                // 시리즈 초기화
                Invoke((MethodInvoker)delegate
                {
                    chart_trace.Series[0].Points.Clear();
                    for (int i = 0; i < 691; i++)
                    {
                        chart_trace.Series[0].Points.AddY(0.0);
                    }
                });


                while (true)
                {

                    //command = "INIT:CONM";
                    //session.Write(command);

                    command = "CALC:MARK1:MAX:PEAK";
                    session.Write(command);

                    command = "CALC:MARK1:X?";
                    double resultX = Convert.ToDouble(session.Query(command));
                    data_convert con = convert(resultX);

                    command = "CALC:MARK1:Y?";
                    double resultY = Convert.ToDouble(session.Query(command));
                    resultY = Math.Round(resultY, 2);

                    //Console.WriteLine("resultY : " + resultY);

                    command = "TRAC? TRACE1";
                    session.Write(command);

                    // 데이터가 바이너리로 넘어오는 경우
                    byte[] bytes = session.ReadByteArray();

                    char tmp = (char)bytes[0];
                    if (tmp.ToString() == "#")
                    {
                        tmp = (char)bytes[1];

                        int length = Convert.ToInt16(tmp.ToString());

                        string datalength = ((char)bytes[2]).ToString();
                        datalength += ((char)bytes[3]).ToString();
                        datalength += ((char)bytes[4]).ToString();
                        datalength += ((char)bytes[5]).ToString();

                        int datalength_int = Convert.ToInt32(datalength);

                        double[] values = new double[datalength_int / length];
                        Invoke((MethodInvoker)delegate
                        {
                            try
                            {
                                label_peakfreq.Text = con.data.ToString("F6", CultureInfo.InvariantCulture) + " " + con.unit;
                                label_peaklevel.Text = resultY.ToString("F2", CultureInfo.InvariantCulture) + " dBm";
                                //label_peakfreq.Refresh();
                                //label_peaklevel.Refresh();

                                //chart_trace.Series[0].Points.Clear();

                                // TX/RX 판별
                                string txrx = "";

                                Decimal tx_upper = radSpinEditor_tx.Value + radSpinEditor_tx_limit.Value;
                                Decimal tx_lower = radSpinEditor_tx.Value - radSpinEditor_tx_limit.Value;
                                Decimal rx_upper = radSpinEditor_rx.Value + radSpinEditor_rx_limit.Value;
                                Decimal rx_lower = radSpinEditor_rx.Value - radSpinEditor_rx_limit.Value;

                                Decimal dBm = Convert.ToDecimal(resultY);
                                if (dBm >= tx_lower && dBm <= tx_upper)
                                {
                                    txrx = "TX";
                                    radPanel_tx.Visible = true;
                                }
                                else if (dBm >= rx_lower && dBm <= rx_upper)
                                {
                                    txrx = "RX";
                                    radPanel_rx.Visible = true;
                                }
                                else
                                {
                                    txrx = "NONE";
                                    radPanel_tx.Visible = false;
                                    radPanel_rx.Visible = false;
                                }

                                // Chart 그리기
                                if (checkBox_update.Checked)
                                {

                                    examplePointPairLitst.Clear();
                                    chart_trace.Series.SuspendUpdates();
                                    chart_trace.Series[0].Points.SuspendUpdates();
                                    for (int i = 0; i < values.Length; i++)
                                    {
                                        if (length == 4)
                                            values[i] = BitConverter.ToSingle(bytes, i * length + 6);
                                        else if (length == 8)
                                            values[i] = BitConverter.ToDouble(bytes, i * length + 6);
                                        //chart_trace.Series[0].Points.AddY(values[i]);
                                        chart_trace.Series[0].Points[i].SetValueY(values[i]);

                                        //examplePointPairLitst.Add(i, values[i]);

                                    }

                                    //zedGraphControl1.AxisChange();
                                    //zedGraphControl1.Invalidate();
                                    //zedGraphControl1.Update();

                                    chart_trace.Series[0].Points.ResumeUpdates();

                                    chart_trace.Series.ResumeUpdates();
                                    chart_trace.Update();


                                    chart_peak.Series.SuspendUpdates();
                                    chart_peak.Series[0].Points.AddXY(DateTime.Now.ToShortTimeString(), resultY);
                                    chart_peak.Series.ResumeUpdates();

                                    if (txrx == "TX")
                                    {
                                        System.Windows.Forms.DataVisualization.Charting.DataPoint pt = chart_peak.Series[0].Points[chart_peak.Series[0].Points.Count - 1];
                                        pt.MarkerColor = System.Drawing.Color.DeepSkyBlue;
                                        pt.MarkerSize = 10;
                                        pt.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle;

                                    }
                                    else if (txrx == "RX")
                                    {
                                        System.Windows.Forms.DataVisualization.Charting.DataPoint pt = chart_peak.Series[0].Points[chart_peak.Series[0].Points.Count - 1];
                                        pt.MarkerColor = System.Drawing.Color.Red;
                                        pt.MarkerSize = 10;
                                        pt.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle;

                                    }

                                    if (chart_peak.Series[0].Points.Count > 500)
                                    {
                                        chart_peak.Series[0].Points.RemoveAt(0);
                                    }

                                    if (chart_peak.ChartAreas[0].AxisX.Maximum > chart_peak.ChartAreas[0].AxisX.ScaleView.Size)
                                    {
                                        chart_peak.ChartAreas[0].AxisX.ScaleView.Scroll(chart_peak.ChartAreas[0].AxisX.Maximum);
                                    }

                                }

                                // data insert
                                if (resultY >= Convert.ToDouble(radSpinEditor_rec_limit.Value))
                                {
                                    radPanel_rec.Visible = true;

                                    FSV_DATA fsv_data = new FSV_DATA();
                                    fsv_data.TXRX = txrx;
                                    fsv_data.freq = Convert.ToDouble(comboBox_freq.Text);
                                    fsv_data.peak_freq = resultX;
                                    fsv_data.peak_level = resultY;
                                    fsv_data.trace_data = bytes;

                                    if (today_filename != DateTime.Now.ToShortDateString())
                                    {
                                        today_filename = DateTime.Now.ToShortDateString();
                                        Database.Connect();
                                    }
                                    Database.insert_fsv_data(fsv_data);
                                }
                                else
                                {
                                    radPanel_rec.Visible = false;
                                }

                                rec_count += 1;

                                if (sw.ElapsedMilliseconds > 1000)
                                {
                                    label_rec_count.Text = rec_count.ToString() + " Captures/Sec";
                                    label_rec_count.Refresh();
                                    rec_count = 0;
                                    sw.Restart();
                                }

                            }
                            catch (Exception ex)
                            {
                                //RadMessageBox.Show(ex.Message);
                            }

                        });
                    }

                }
            }
            catch (Exception e)
            {

            }
        }



        private void radButton_Stop_Click(object sender, EventArgs e)
        {
            measurement.Abort();
            radButton_Start.Enabled = true;
            radButton_Stop.Enabled = false;
            radButton_setup.Enabled = true;
            radButton_get_setting.Enabled = true;

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (measurement != null)
            {
                if (measurement.ThreadState == System.Threading.ThreadState.Running)
                {
                    measurement.Abort();
                }
            }
            if (session != null)
            {
                session.Dispose();
            }
        }

        private void chData_MouseWheel(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Delta < 0)
                {
                    chart_trace.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                    chart_trace.ChartAreas[0].AxisY.ScaleView.ZoomReset();
                }

                if (e.Delta > 0)
                {
                    double xMin = chart_trace.ChartAreas[0].AxisX.ScaleView.ViewMinimum;
                    double xMax = chart_trace.ChartAreas[0].AxisX.ScaleView.ViewMaximum;
                    double yMin = chart_trace.ChartAreas[0].AxisY.ScaleView.ViewMinimum;
                    double yMax = chart_trace.ChartAreas[0].AxisY.ScaleView.ViewMaximum;

                    double posXStart = chart_trace.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) - (xMax - xMin) / 4;
                    double posXFinish = chart_trace.ChartAreas[0].AxisX.PixelPositionToValue(e.Location.X) + (xMax - xMin) / 4;
                    double posYStart = chart_trace.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) - (yMax - yMin) / 4;
                    double posYFinish = chart_trace.ChartAreas[0].AxisY.PixelPositionToValue(e.Location.Y) + (yMax - yMin) / 4;

                    chart_trace.ChartAreas[0].AxisX.ScaleView.Zoom(posXStart, posXFinish);
                    chart_trace.ChartAreas[0].AxisY.ScaleView.Zoom(posYStart, posYFinish);
                }
            }
            catch { }
        }

        private void radButton_viewdata_Click(object sender, EventArgs e)
        {
            license_check();
            DataForm dataform = new DataForm();
            dataform.Show();
        }

        private void comboBox_freq_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (session != null)
            {
                license_check();
                string command = "SENSe:FREQuency:CENTer " + comboBox_freq.Text + comboBox_freq_unit.Text;
                session.Write(command);
            }
        }

        private void radButton2_Click(object sender, EventArgs e)
        {
            set_config();
            RadMessageBox.Show("Saved configuration!");
        }

        private void lightningChartUltimate1_Load(object sender, EventArgs e)
        {

        }

        private void comboBox_freq_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (session != null)
                {
                    license_check();
                    string command = "SENSe:FREQuency:CENTer " + comboBox_freq.Text + comboBox_freq_unit.Text;
                    session.Write(command);
                }
            }
        }

        private void radSpinEditor_tx_ValueChanged(object sender, EventArgs e)
        {
            check_limit();
        }


        private void radSpinEditor_tx_limit_ValueChanged(object sender, EventArgs e)
        {
            check_limit();
        }

        private void radSpinEditor_rx_ValueChanged(object sender, EventArgs e)
        {
            check_limit();
        }

        private void radSpinEditor_rx_limit_ValueChanged(object sender, EventArgs e)
        {
            check_limit();
        }

        private void check_limit()
        {
            decimal rx_max;
            decimal rx_min;
            decimal tx_max;
            decimal tx_min;

            rx_max = radSpinEditor_rx.Value + radSpinEditor_rx_limit.Value;
            rx_min = radSpinEditor_rx.Value - radSpinEditor_rx_limit.Value;
            tx_max = radSpinEditor_tx.Value + radSpinEditor_tx_limit.Value;
            tx_min = radSpinEditor_tx.Value - radSpinEditor_tx_limit.Value;

            if (rx_max > tx_min)
            {
                label_limit.Visible = true;
            }
            else
            {
                label_limit.Visible = false;
            }
        }

        private void PrepareProcess()
        {
            exampleGraphPane = new GraphPane();
            examplePointPairLitst = new PointPairList();

            MakeChart();
        }
        private void MakeChart()
        {
            int lineWidth = 1;
            exampleGraphPane = zedGraphControl1.GraphPane;

            exampleGraphPane.Title.Text = "EXAMPLE FOR ZEDGRAPH";
            exampleGraphPane.Title.IsVisible = false;//그래프 타이틀이 보기싫으면 false. default는 true;
            exampleGraphPane.XAxis.Type = ZedGraph.AxisType.Linear;
            exampleLineItem = exampleGraphPane.AddCurve("FSV Trace", examplePointPairLitst, Color.Yellow, SymbolType.None);

            exampleLineItem.Line.Width = lineWidth;
            exampleLineItem.Symbol.Fill = new ZedGraph.Fill(Color.Black);

            exampleGraphPane.XAxis.MajorGrid.IsVisible = true;
            exampleGraphPane.YAxis.MajorGrid.IsVisible = true;
            exampleGraphPane.XAxis.MajorGrid.Color = Color.White;
            exampleGraphPane.YAxis.MajorGrid.Color = Color.White;

            exampleGraphPane.XAxis.Scale.Min = 0;
            exampleGraphPane.XAxis.Scale.Max = 691;
            exampleGraphPane.YAxis.Scale.Min = -130;
            exampleGraphPane.YAxis.Scale.Max = -50;

            exampleGraphPane.Chart.Fill = new ZedGraph.Fill(Color.Black);
            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();
        }
    }
}
