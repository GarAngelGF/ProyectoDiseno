using Microsoft.Data.SqlClient;
using System.Data; // Importante: Se requiere para leer el ConnectionState

namespace ProyectoDiseño.Patrones
{
    public class DatabaseConnection
    {
        // Variable estática que almacena la única instancia de esta clase
        private static DatabaseConnection _instancia = null;
        private static readonly object _bloqueo = new object();
        private readonly string _cadenaConexion = "Server=tcp:adminbdnonato26.database.windows.net,1433;Initial Catalog=SICI_ElJardin;Persist Security Info=False;User ID=adminprobd2026;Password=Birriamasters2026#;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

        // 1. SOLUCIÓN: Variable para almacenar LA ÚNICA instancia de SqlConnection
        private SqlConnection _conexionUnica;

        private DatabaseConnection()
        {
            // 2. SOLUCIÓN: La conexión a la BD se crea UNA SOLA VEZ cuando nace el Singleton
            _conexionUnica = new SqlConnection(_cadenaConexion);
        }

        public static DatabaseConnection Instancia
        {
            get
            {
                lock (_bloqueo)
                {
                    if (_instancia == null)
                    {
                        _instancia = new DatabaseConnection();
                    }
                    return _instancia;
                }
            }
        }

        // 3. SOLUCIÓN: Retorna siempre la misma instancia en lugar de hacer un "new"
        public SqlConnection ObtenerConexion()
        {
            // Garantizamos que la conexión única esté abierta antes de entregarla a los controladores
            if (_conexionUnica.State == ConnectionState.Closed)
            {
                _conexionUnica.Open();
            }

            return _conexionUnica;
        }
    }
}