using MySql.Data.MySqlClient;
using System;
using System.Data;

namespace PineTree.Classes
{
    public static class DB
    {
        private static string GetMySqlDBConfig()
        {
            string sqlServer = "211.45.163.175";
            string sqlDatabase = "PineTree";
            string sqlUid = "root";
            string sqlPassword = "briansk!@";

            return "Server=" + sqlServer
                + ";Database=" + sqlDatabase
                + ";Uid=" + sqlUid
                + ";Pwd=" + sqlPassword
                + ";Charset=utf8mb4";
        }

        public static MySqlConnection CreateConnection()
        {
            return new MySqlConnection(GetMySqlDBConfig());
        }

        public static DataTable MySqlQueryRead(string query, ParamValuePair[] parameters)
        {
            using (MySqlConnection conn = new MySqlConnection(GetMySqlDBConfig()))
            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.CommandType = CommandType.Text;
                AddParameters(cmd, parameters);

                using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
        }

        public static DataTable MySqlCallProcedure(string sQuery)
        {
            DataSet ds = MySqlQueryRead(sQuery);
            return ds.Tables[0];
        }

        public static DataSet MySqlQueryRead(string strQuery)
        {
            DataSet dt = new DataSet();
            try
            {
                MySqlConnection conn = new MySqlConnection(GetMySqlDBConfig());
                MySqlDataAdapter adapter = new MySqlDataAdapter(strQuery, conn);
                DataSet customers = new DataSet();
                dt = new DataSet();
                adapter.Fill(dt, "tbl");

                conn.Close();
            }
            catch (Exception ex)
            {
                string s = ex.Message;
            }

            return dt;

        }

        public static void MySqlQueryExecute(string strQuery)
        {

            //string sConnect = getDBConfig;
            MySqlConnection conn = new MySqlConnection(GetMySqlDBConfig());


            //Execute
            conn.Open();
            MySqlCommand comm = new MySqlCommand(strQuery, conn);

            //for¹®
            //comm.Parameters.AddWithValue("@PlantCode",PlantCode);

            //for¹®

            comm.ExecuteNonQuery();
            conn.Close();


        }

        public static int Execute(string query, ParamValuePair[] parameters)
        {
            using (MySqlConnection conn = new MySqlConnection(GetMySqlDBConfig()))
            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.CommandType = CommandType.Text;
                AddParameters(cmd, parameters);

                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public static object Scalar(string query, ParamValuePair[] parameters)
        {
            using (MySqlConnection conn = new MySqlConnection(GetMySqlDBConfig()))
            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.CommandType = CommandType.Text;
                AddParameters(cmd, parameters);

                conn.Open();
                return cmd.ExecuteScalar();
            }
        }

        private static void AddParameters(MySqlCommand cmd, ParamValuePair[] parameters)
        {
            if (parameters == null)
            {
                return;
            }

            foreach (ParamValuePair parameter in parameters)
            {
                cmd.Parameters.AddWithValue(parameter.ParameterName, parameter.Value ?? DBNull.Value);
            }
        }
    }

    public class ParamValuePair
    {
        public ParamValuePair()
        {
        }

        public ParamValuePair(string parameterName, object value)
        {
            ParameterName = parameterName;
            Value = value ?? DBNull.Value;
        }

        public string ParameterName { get; set; }

        public object Value { get; set; }
    }
}
