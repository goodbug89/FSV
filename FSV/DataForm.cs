using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Telerik.WinControls;
using Telerik.WinControls.UI.Export;
using Telerik.Data;
using System.Windows.Forms.DataVisualization.Charting;

namespace FSV
{
    public partial class DataForm : Telerik.WinControls.UI.RadForm
    {
        public DataForm()
        {
            InitializeComponent();
        }

        private void DataForm_Load(object sender, EventArgs e)
        {
            radDateTimePicker1.Value = DateTime.Now.AddHours(-1);
            radTimePicker1.Value = DateTime.Now.AddHours(-1);

            radGridView1.Columns.Add("sq", "SQ", "sq");
            radGridView1.Columns["sq"].Width = 100;
            radGridView1.Columns["sq"].TextAlignment = ContentAlignment.MiddleCenter;


            radGridView1.Columns.Add("create_datetime", "등록일시", "create_datetime");
            radGridView1.Columns["create_datetime"].Width = 200;
            radGridView1.Columns["create_datetime"].FormatString = "{0:yyyy-MM-dd HH:mm:ss.fff}";
            radGridView1.Columns["create_datetime"].TextAlignment = ContentAlignment.MiddleCenter;

            radGridView1.Columns.Add("txrx", "구분", "txrx");
            radGridView1.Columns["txrx"].Width = 100;
            radGridView1.Columns["txrx"].TextAlignment = ContentAlignment.MiddleCenter;

            radGridView1.Columns.Add("freq", "주파수", "freq");
            radGridView1.Columns["freq"].Width = 150;
            radGridView1.Columns["freq"].TextAlignment = ContentAlignment.MiddleRight;

            radGridView1.Columns.Add("peak_freq", "PEAK 주파수", "peak_freq");
            radGridView1.Columns["peak_freq"].Width = 150;
            radGridView1.Columns["peak_freq"].FormatString = "{0:N0}";
            radGridView1.Columns["peak_freq"].TextAlignment = ContentAlignment.MiddleRight;

            radGridView1.Columns.Add("peak_level", "PEAK LEVEL", "peak_level");
            radGridView1.Columns["peak_level"].Width = 150;
            radGridView1.Columns["peak_level"].FormatString = "{0:N2}";
            radGridView1.Columns["peak_level"].TextAlignment = ContentAlignment.MiddleRight;

            radGridView1.EnableFiltering = true;
            radGridView1.MasterTemplate.ShowHeaderCellButtons = true;
            radGridView1.MasterTemplate.ShowFilteringRow = false;

        }
        private void ChartCursorSelected(double x, double y)
        {
            int i = Convert.ToInt32(x);
            if (i >= 0 && i < radGridView1.RowCount)
            {
                radGridView1.CurrentRow = radGridView1.Rows[i];

                string peak = Convert.ToString(radGridView1.SelectedRows[0].Cells["peak_level"].Value);
                string time = Convert.ToDateTime(radGridView1.SelectedRows[0].Cells["create_datetime"].Value).ToLongTimeString();
                
                toolStripStatusLabelX.Text = time + " :: " + peak + " dBm";
            }
        }
        private void ChartCursorMoved(double x, double y)
        {
            int i = Convert.ToInt32(x);
            if (i >= 0 && i < radGridView1.RowCount)
            {
                radGridView1.CurrentRow = radGridView1.Rows[i];

                string peak = Convert.ToString(radGridView1.SelectedRows[0].Cells["peak_level"].Value);
                string time = Convert.ToDateTime(radGridView1.SelectedRows[0].Cells["create_datetime"].Value).ToLongTimeString();

                toolStripStatusLabelY.Text = time + " :: " + peak + " dBm";
            }
        }

        private void ChartCursorSelected_trace(double x, double y)
        {
                
        }
        private void ChartCursorMoved_trace(double x, double y)
        {
                toolStripStatusLabel1.Text = y.ToString() + " dBm";
        }
        private void radDateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            
        }

        private void radGridView1_SelectionChanged(object sender, EventArgs e)
        {
            if (radGridView1.SelectedRows.Count > 0)
            {
                int sq = Convert.ToInt32(radGridView1.SelectedRows[0].Cells["sq"].Value);
                double peak = Convert.ToDouble(radGridView1.SelectedRows[0].Cells["peak_level"].Value);
                double peak_freq = Convert.ToDouble(radGridView1.SelectedRows[0].Cells["peak_freq"].Value);

                byte[] bytes = Database.get_chart_data(sq);

                if (bytes != null)
                {

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



                        chart_trace.Series[0].Points.Clear();


                        for (int i = 0; i < values.Length; i++)
                        {
                            if (length == 4)
                                values[i] = BitConverter.ToSingle(bytes, i * length + 6);
                            else if (length == 8)
                                values[i] = BitConverter.ToDouble(bytes, i * length + 6);

                            chart_trace.Series[0].Points.AddY(values[i]);

                        }

                        double interestingValue = peak;
                        double limit = 0.01;
                        foreach (var pt in chart_trace.Series[0].Points)
                        {
                            if (pt.YValues[0] >= interestingValue - limit && pt.YValues[0] <= interestingValue + limit)
                            {
                                pt.MarkerColor = System.Drawing.Color.Red;
                                pt.MarkerSize = 5;
                                pt.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Diamond;
                                pt.ToolTip = "LEVEL : " + peak.ToString("N2");
                            }
                        }

                        chart_trace.Update();
                    }
                }
            }
        }

        private void radButton_search_Click(object sender, EventArgs e)
        {
            Database.View_Connect(radDateTimePicker1.Value.ToShortDateString());
            DateTime start_date;
            DateTime end_date;

            start_date = DateTime.Parse(radDateTimePicker1.Value.ToShortDateString() + " " + radTimePicker1.Value.Value.ToShortTimeString());
            end_date = DateTime.Parse(radDateTimePicker1.Value.ToShortDateString() + " " + radTimePicker1.Value.Value.AddHours(1).ToShortTimeString());

            DataTable data = Database.get_data(start_date,end_date);
            for (int i = 0; i < data.Rows.Count;i++)
            {
                double peak_level = Convert.ToDouble(data.Rows[i]["peak_level"]);
                DateTime time = Convert.ToDateTime(data.Rows[i]["create_datetime"]);
                chart_peak.Series[0].Points.AddXY(time.ToShortTimeString(), peak_level);
            }
            radGridView1.DataSource = data;
            label_reccount.Text = data.Rows.Count.ToString() + " Records";

            chart_peak.EnableZoomAndPanControls(ChartCursorSelected, ChartCursorMoved);
            chart_trace.EnableZoomAndPanControls(ChartCursorSelected_trace, ChartCursorMoved_trace);

        }

        private void radButton_savescreen_Click(object sender, EventArgs e)
        {
            if (radGridView1.SelectedRows.Count > 0)
            {
                ScreenCapture sc = new ScreenCapture();
                string filename = Convert.ToDateTime(radGridView1.SelectedRows[0].Cells["create_datetime"].Value).ToString("yyyy-MM-dd HHmmss.fff");
                sc.CaptureWindowToFile(this.Handle, ".\\capture\\screen-" + filename + ".png", ImageFormat.Png);
                RadMessageBox.Show("screen-" + filename + ".png Saved!!");
            }
            else
            {
                RadMessageBox.Show("Select row first!!");
            }
        }

        private void radButton_csavechart_Click(object sender, EventArgs e)
        {
            if (radGridView1.SelectedRows.Count > 0)
            {
                ScreenCapture sc = new ScreenCapture();
                string filename = Convert.ToDateTime(radGridView1.SelectedRows[0].Cells["create_datetime"].Value).ToString("yyyy-MM-dd HHmmss.fff");
                sc.CaptureWindowToFile(chart_trace.Handle, ".\\capture\\tracechart-" + filename + ".png", ImageFormat.Png);
                RadMessageBox.Show("tracechart-" + filename + ".png Saved!!");
            }
            else
            {
                RadMessageBox.Show("Select row first!!");
            }
        }

        private void radButton_peak_chart_Click(object sender, EventArgs e)
        {
            if (radGridView1.SelectedRows.Count > 0)
            {
                ScreenCapture sc = new ScreenCapture();
                string filename = DateTime.Now.ToString("yyyy-MM-dd HHmmss.fff");
                sc.CaptureWindowToFile(chart_peak.Handle, ".\\capture\\peakchart-" + filename + ".png", ImageFormat.Png);
                RadMessageBox.Show("peakchart-" + filename + ".png Saved!!");
            }
            else
            {
                RadMessageBox.Show("Select row first!!");
            }
        }

        private void radButton_excel_Click(object sender, EventArgs e)
        {
            string filename = DateTime.Now.ToString("yyyy-MM-dd HHmmss.fff");
            ExportToExcelML exporter = new ExportToExcelML(this.radGridView1);
            exporter.RunExport(".\\capture\\data-" + filename + ".xls");
            RadMessageBox.Show("data-" + filename + ".xls Saved!!");
        }
    }
}
