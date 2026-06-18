using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Models;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.ViewModels;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace ProyectoDiseño.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home/Index (Pantalla de Login)
        [HttpGet]
        public IActionResult Index()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UsuarioRol")))
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // POST: Home/Login
        [HttpPost]
        public IActionResult Login(string usuario, string contrasena)
        {
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            try
            {
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

        // GET: Home/Dashboard
        [HttpGet]
        public IActionResult Dashboard()
        {
            string rolUsuario = HttpContext.Session.GetString("UsuarioRol");

            if (string.IsNullOrEmpty(rolUsuario))
            {
                return RedirectToAction("Index");
            }

            var inventario = ObtenerInventario();
            var alertas = inventario.FindAll(i => i.CantidadActual < i.StockMinimo);

            var viewModel = new DashboardViewModel
            {
                InventarioCompleto = inventario,
                AlertasStock = alertas,
                // SOLUCIÓN PUNTO 2: Carga de movimientos históricos al ViewModel
                HistorialMovimientos = ObtenerHistorialMovimientos()
            };

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
            HttpContext.Session.Clear();
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

        // =========================================================================
        // SOLUCIÓN PUNTO 2: MÉTODOS AUXILIARES Y CONSULTA DE AUDITORÍA (JOIN)
        // =========================================================================
        private List<MovimientoInventario> ObtenerHistorialMovimientos()
        {
            var lista = new List<MovimientoInventario>();
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            // Consulta relacional con JOIN para traer los nombres descriptivos en lugar de solo los identificadores numéricos
            string query = @"SELECT m.IdMovimiento, m.IdInsumo, i.Nombre, m.IdPersona, p.NombreCompleto, 
                                    m.TipoMovimiento, m.Cantidad, m.FechaMovimiento, m.Observaciones
                             FROM MovimientoInventario m
                             INNER JOIN Insumo i ON m.IdInsumo = i.IdInsumo
                             INNER JOIN Persona p ON m.IdPersona = p.IdPersona
                             ORDER BY m.FechaMovimiento DESC";

            using (SqlCommand cmd = new SqlCommand(query, conexion))
            {
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new MovimientoInventario
                        {
                            IdMovimiento = reader.GetInt32(0),
                            IdInsumo = reader.GetInt32(1),
                            NombreInsumo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            IdPersona = reader.GetInt32(3),
                            NombrePersona = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            TipoMovimiento = reader.GetString(5),
                            Cantidad = reader.GetDecimal(6),
                            FechaMovimiento = reader.GetDateTime(7),
                            Observaciones = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                        });
                    }
                }
            }
            return lista;
        }
    }
}