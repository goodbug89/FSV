using FirebirdSql.Data.FirebirdClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Telerik.WinControls;

namespace FSV
{
    public class FSV_DATA
    {
        public int sq;
        public DateTime create_datetime;
        public string TXRX;
        public double freq;
        public double peak_freq;
        public double peak_level;
        public byte[] trace_data;
    }

    class Database
    {
        public static FbConnection connection;
        public static FbConnection View_connection;

        public static void Connect()
        {
            try
            {
                //string today_filename = DateTime.Now.ToShortDateString();
                FileInfo fi = new FileInfo(".\\data\\" + MainForm.today_filename + ".fdb");
                if (!fi.Exists)
                {
                    CreateDatabase();
                }
                string connectString =
                    "Database=" + ".\\data\\" + MainForm.today_filename + ".fdb;" +
                    "User=SYSDBA;" +
                    "Password=masterkey;" +
                    "Dialect=3;" +
                    "Charset=UTF8;" +
                    "ServerType=1";
                connection = new FbConnection(connectString);
                connection.Open();
            }
            catch (Exception ex)
            {
                RadMessageBox.Show("Database File open error!!");
            }
            //inserttest();

        }


        public static void View_Connect(string datestring)
        {
            try
            {
                //string today_filename = DateTime.Now.ToShortDateString();
                FileInfo fi = new FileInfo(".\\data\\" + datestring + ".fdb");
                if (!fi.Exists)
                {
                    RadMessageBox.Show("No Database File!!");
                }
                else
                {
                    string connectString =
                        "Database=" + ".\\data\\" + datestring + ".fdb;" +
                        "User=SYSDBA;" +
                        "Password=masterkey;" +
                        "Dialect=3;" +
                        "Charset=UTF8;" +
                        "ServerType=1";
                    View_connection = new FbConnection(connectString);
                    View_connection.Open();
                }
            }
            catch (Exception ex)
            {
                RadMessageBox.Show("Database File open error!!");
            }
            //inserttest();

        }
        public static void CreateDatabase()
        {
            string today_filename = DateTime.Now.ToShortDateString();
            string connectString =
                           "Database=" + ".\\data\\" + today_filename + ".fdb;" +
                           "User=SYSDBA;" +
                           "Password=masterkey;" +
                           "Dialect=3;" +
                           "Charset=UTF8;" +
                           "ServerType=1";
            try
            {
                FbConnection.CreateDatabase(connectString);
                connection = new FbConnection(connectString);
                connection.Open();
                CreateTables();
                connection.Close();
            }
            catch (Exception ex)
            {
                RadMessageBox.Show("Database File create error!!");
            }
        }


        public static void CreateTables()
        {
            try
            {
                // Create Config_master table.
                FbCommand myCommand = new FbCommand();
                myCommand.Connection = connection;



                // Create Master table.
                myCommand.CommandText =
                        "CREATE TABLE FSV_DATA (" +
                        "    SQ INTEGER NOT NULL," +
                        "    CREATE_DATETIME TIMESTAMP DEFAULT 'now' NOT NULL," +
                        "    TXRX VARCHAR(10)," +
                        "    freq FLOAT," +
                        "    peak_freq FLOAT," +
                        "    peak_level FLOAT," +
                        "    trace_data BLOB );";


                myCommand.ExecuteNonQuery();
                myCommand.CommandText = "ALTER TABLE FSV_DATA ADD CONSTRAINT PK_FSV_DATA PRIMARY KEY (SQ);";
                myCommand.ExecuteNonQuery();

                myCommand.CommandText = "CREATE INDEX IDX_CREATE_DATETIME ON FSV_DATA (CREATE_DATETIME);";
                myCommand.ExecuteNonQuery();

                myCommand.CommandText = "CREATE INDEX IDX_TXRX ON FSV_DATA (TXRX);";
                myCommand.ExecuteNonQuery();

                myCommand.CommandText = "CREATE INDEX IDX_freq ON FSV_DATA (freq);";
                myCommand.ExecuteNonQuery();

                myCommand.CommandText = "CREATE GENERATOR GEN_FSV_DATA_SQ;";
                myCommand.ExecuteNonQuery();

                myCommand.CommandText = "CREATE OR ALTER TRIGGER FSV_DATA_BI FOR FSV_DATA ACTIVE BEFORE INSERT POSITION 0 as begin if (new.sq is null) then new.sq = gen_id(GEN_FSV_DATA_SQ,1); end";
                myCommand.ExecuteNonQuery();

                myCommand.Dispose();
            }
            catch (Exception ex)
            {
                RadMessageBox.Show("Table create error!!");
            }

        }


        public static void insert_fsv_data(FSV_DATA fsv_data)
        {
            
            try
            {
                FbCommand myCommand = new FbCommand();
                myCommand.Connection = connection;
                

                myCommand.CommandText = "insert into fsv_data(TXRX,freq,peak_freq,peak_level,trace_data) values(@TXRX,@freq,@peak_freq,@peak_level,@trace_data);";
                myCommand.Parameters.Clear();
                myCommand.Parameters.Add("TXRX", FbDbType.VarChar, 10);
                myCommand.Parameters.Add("freq", FbDbType.Double, 0);
                myCommand.Parameters.Add("peak_freq", FbDbType.Double, 0);
                myCommand.Parameters.Add("peak_level", FbDbType.Double, 0);
                myCommand.Parameters.Add("trace_data", FbDbType.Binary, 32765);

                myCommand.Parameters[0].Value = fsv_data.TXRX.Trim();
                myCommand.Parameters[1].Value = fsv_data.freq;
                myCommand.Parameters[2].Value = fsv_data.peak_freq;
                myCommand.Parameters[3].Value = fsv_data.peak_level;
                myCommand.Parameters[4].Value = fsv_data.trace_data;

                myCommand.ExecuteNonQuery();
                
                myCommand.Dispose();
                
            }
            catch (Exception e)
            {
                RadMessageBox.Show("Insert error!!");
            }
            
        }

        public static DataTable get_data(DateTime start_date,DateTime end_date)
        {
            DataTable result = new DataTable();

            FbCommand myCommand = new FbCommand();
            myCommand.Connection = View_connection;
            myCommand.CommandText = "select sq,create_datetime,txrx,freq,peak_freq,peak_level from fsv_data where CREATE_DATETIME >= @startdate and CREATE_DATETIME < @enddate order by sq;";
            myCommand.Parameters.Clear();
            myCommand.Parameters.Add("startdate", FbDbType.TimeStamp, 10);
            myCommand.Parameters.Add("enddate", FbDbType.TimeStamp, 10);
            myCommand.Parameters[0].Value = start_date;
            myCommand.Parameters[1].Value = end_date;
            // Get the results
            using (FbDataReader sqlReader = myCommand.ExecuteReader())
            {
                // Load the results into the table
                result.Load(sqlReader);
            }

            return result;

        }


        public static byte[] get_chart_data(int sq)
        {
            byte[] result_byte = null;
            DataTable result = new DataTable();

            FbCommand myCommand = new FbCommand();
            myCommand.Connection = View_connection;
            myCommand.CommandText = "select trace_data from fsv_data where sq=@sq;";
            myCommand.Parameters.Clear();
            myCommand.Parameters.Add("sq", FbDbType.Integer, 10);
            myCommand.Parameters[0].Value = sq;
            // Get the results
            using (FbDataReader sqlReader = myCommand.ExecuteReader())
            {
                // Load the results into the table
                result.Load(sqlReader);
            }
            if (result.Rows.Count > 0)
            {
                result_byte = (byte[])(result.Rows[0][0]);
            }
            return result_byte;

        }

    }
}
