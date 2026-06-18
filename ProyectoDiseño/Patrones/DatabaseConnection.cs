using Microsoft.Data.SqlClient;

namespace ProyectoDiseño.Patrones
{
    public class DatabaseConnection
    {
        // Variable estática que almacena la única instancia
        private static DatabaseConnection _instancia = null;

        // Objeto de bloqueo para garantizar seguridad en hilos (Thread-safe)
        private static readonly object _bloqueo = new object();

        // Cadena de conexión (Idealmente la obtienes de appsettings.json)
        private readonly string _cadenaConexion = "Server=adminbdnonato26.database.windows.net;Database=SICI_ElJardin;Trusted_Connection=True;MultipleActiveResultSets=true;Encrypt=False;";

        // Constructor privado para evitar que otras clases usen 'new'
        private DatabaseConnection() { }

        // Propiedad pública estática para obtener la instancia
        public static DatabaseConnection Instancia
        {
            get
            {
                lock (_bloqueo) // Bloquea el hilo para evitar que se creen dos instancias al mismo tiempo
                {
                    if (_instancia == null)
                    {
                        _instancia = new DatabaseConnection();
                    }
                    return _instancia;
                }
            }
        }

        // Método para entregar el objeto SqlConnection
        public SqlConnection ObtenerConexion()
        {
            return new SqlConnection(_cadenaConexion);
        }
    }
}
