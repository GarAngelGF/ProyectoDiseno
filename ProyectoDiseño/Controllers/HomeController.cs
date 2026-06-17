using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Models;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.ViewModels;
using System.Collections.Generic;

namespace ProyectoDiseño.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index(string rolUsuario) // 'rolUsuario' vendría de la sesión activa
        {
            var inventario = ObtenerInventario();

            // Filtramos los insumos que requieren alerta usando LINQ
            var alertas = inventario.FindAll(i => i.CantidadActual < i.StockMinimo);

            var viewModel = new DashboardViewModel
            {
                InventarioCompleto = inventario,
                AlertasStock = alertas
            };

            // Redirección basada en el rol de la Persona
            if (rolUsuario == "Cocina")
            {
                return View("VistaCocina", viewModel);
            }

            // Vista por defecto para el Administrador
            return View("DashboardAdmin", viewModel);
        }

        private List<Insumo> ObtenerInventario()
        {
            var lista = new List<Insumo>();
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            using (conexion)
            {
                conexion.Open();
                string query = "SELECT IdInsumo, Nombre, UnidadMedida, CantidadActual, StockMinimo FROM Insumo WHERE Activo = 1";
                using (SqlCommand cmd = new SqlCommand(query, conexion))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new Insumo
                            {
                                IdInsumo = reader.GetInt32(0),
                                Nombre = reader.GetString(1),
                                UnidadMedida = reader.GetString(2),
                                CantidadActual = reader.GetDecimal(3),
                                StockMinimo = reader.GetDecimal(4)
                            });
                        }
                    }
                }
            }
            return lista;
        }
    }
}
