using FechasCarnets.SVCCAACR;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace FechasCarnets
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private static string ConnectionString => ConfigurationManager.ConnectionStrings["basedatos"].ConnectionString;

        private void Form1_Load(object sender, EventArgs e)
        {
            
                var auth = new WebAuthentication
                {
                    UserCode = "jhernandez",
                    Password = "colegio"
                };

                using (var servicio = new BusinessPartnerServiceClient())
                {
                    var program = new Form1();
                    DataTable dt = program.ObtenerRegistrosActualizados();
                    servicio.VaciarTablaTemporal(auth);

                    foreach (DataRow row in dt.Rows)
                    {
                    try {    
                    string cardcode = row[0].ToString().PadLeft(6, '0');

                        // Obtiene información actual del carnet
                        var bp = servicio.AgremiadoXCarnet(auth, cardcode);

                        // Actualiza las fechas
                        var response = servicio.ActualizarFechaCarnet(auth, cardcode, Convert.ToDateTime(row[1]), Convert.ToDateTime(row[2]));

                        // Registra el cambio en el log
                        program.RegistrarCambioEnLog(cardcode, bp, row);
                        listBox1.Items.Add(("Resultado de la actualización para "+cardcode+": "+response.Result+""));
                }
            catch (Exception ex)
            {
                    listBox1.Items.Add("Error: " + ex.Message);
                }
            }
                   
                }
            
            StartCloseTimer();

        }
        internal DataTable ObtenerRegistrosActualizados()
        {
            DataTable registros = new DataTable();
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                string query = @"
                    SELECT IDWcarne, IDWfechaexp, IDWFechaVencimiento
                    FROM IDWabogado_nuevo
                    WHERE CONVERT(date, IDWFechaModificacion) = CONVERT(date, GETDATE())";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(registros);
                    }
                }
            }

            return registros;
        }

        internal void RegistrarCambioEnLog(string cardcode, ObtenerAgremiadoResponse bp, DataRow row)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                string query = @"
                    INSERT INTO DATACARD.dbo.LogCambiosFechasSap (
                        fechaRegistro, Carnet, fechaEmisionAnterior, fechaVencimientoAnterior,
                        fechaEmisionNueva, fechaVencmientoNueva
                    ) 
                    VALUES (
                        GETDATE(), @CardCode, @FechaEmisionAnterior, @FechaVencimientoAnterior,
                        @FechaEmisionNueva, @FechaVencimientoNueva
                    )";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CardCode", cardcode);
                    cmd.Parameters.AddWithValue("@FechaEmisionAnterior", GetDbValue(bp.BusinessPartner.EmisionCarnet));
                    cmd.Parameters.AddWithValue("@FechaVencimientoAnterior", GetDbValue(bp.BusinessPartner.VencimientoCarnet));
                    cmd.Parameters.AddWithValue("@FechaEmisionNueva", GetDbValue(row[1]));
                    cmd.Parameters.AddWithValue("@FechaVencimientoNueva", GetDbValue(row[2]));
                    cmd.ExecuteNonQuery();
                }

            }
        }
        private object GetDbValue(object value)
        {
            return value != null && value != DBNull.Value ? Convert.ToDateTime(value) : (object)DBNull.Value;
        }

        private void StartCloseTimer()
        {
            closeTimer = new Timer
            {
                Interval = 20000 // 5 segundos en milisegundos
            };
            closeTimer.Tick += CloseTimer_Tick;
            closeTimer.Start();
        }

        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            closeTimer.Stop();
            closeTimer.Dispose();
            Application.Exit(); // Cierra el formulario
        }

    }
}
