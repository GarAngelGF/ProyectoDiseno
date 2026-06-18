using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Models;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.ViewModels;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http; // Manejo de sesiones

namespace ProyectoDiseño.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home/Index (Pantalla de Inicio de Sesión)
        [HttpGet]
        public IActionResult Index()
        {
            // Si el usuario ya está logueado, lo mandamos directo a su Dashboard
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UsuarioRol")))
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // POST: Home/Login (Procesa el formulario contra la tabla Persona)
        [HttpPost]
        public IActionResult Login(string usuario, string contrasena)
        {
            // CORRECCIÓN: Conexión centralizada única a través del Singleton
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            try
            {
                // Consulta segura para verificar las credenciales de la Persona
                string query = @"SELECT IdPersona, NombreCompleto, Rol 
                                 FROM Persona 
                                 WHERE Usuario = @Usuario AND ContrasenaHash = @Contrasena AND Activo = 1";

                using (SqlCommand cmd = new SqlCommand(query, conexion))
                {
                    cmd.Parameters.AddWithValue("@Usuario", usuario);
                    cmd.Parameters.AddWithValue("@Contrasena", contrasena);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // GUARDAR EN SESIÓN LOS DATOS DEL REGISTRO ENCONTRADO
                            HttpContext.Session.SetInt32("UsuarioId", reader.GetInt32(0));
                            HttpContext.Session.SetString("UsuarioNombre", reader.GetString(1));
                            HttpContext.Session.SetString("UsuarioRol", reader.GetString(2));

                            return RedirectToAction("Dashboard");
                        }
                        else
                        {
                            ViewBag.Error = "Usuario o contraseña incorrectos, o cuenta inactiva.";
                            return View("Index");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error crítico de base de datos: " + ex.Message;
                return View("Index");
            }
        }

        // GET: Home/Dashboard (Aplica restricciones de Rol en el Programa)
        [HttpGet]
        public IActionResult Dashboard()
        {
            string rolUsuario = HttpContext.Session.GetString("UsuarioRol");

            // Restricción: Si no ha iniciado sesión, se bloquea el acceso
            if (string.IsNullOrEmpty(rolUsuario))
            {
                return RedirectToAction("Index");
            }

            var inventario = ObtenerInventario(); var alertas = inventario.FindAll(i => i.CantidadActual < i.StockMinimo);
            var viewModel = new DashboardViewModel
            {
                InventarioCompleto = inventario,
                AlertasStock = alertas
            };

            // CONTROL DE FLUJO Y RESTRICCIONES SEGÚN EL ROL DE LA TABLA PERSONA
            if (rolUsuario == "Cocina")
            {
                return View("VistaCocina", viewModel);
            }
            else if (rolUsuario == "Administrador")
            {
                return View("DashboardAdmin", viewModel);
            }

            return RedirectToAction("Logout");
        }

        // GET: Home/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Destruye la sesión activa
            return RedirectToAction("Index");
        }

        private List<Insumo> ObtenerInventario()
        {
            var lista = new List<Insumo>();
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

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
            return lista;
        }
    }
}