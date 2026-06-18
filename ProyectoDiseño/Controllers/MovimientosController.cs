using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;
using System;
using System.Collections.Generic; // REQUERIDO: Para el manejo de listas de Insumos
using Microsoft.AspNetCore.Http; // REQUERIDO: Para el control de sesiones

namespace ProyectoDiseño.Controllers
{
    public class MovimientosController : Controller
    {
        // Método auxiliar para comprobar la existencia de una sesión activa
        private bool UsuarioEstaAutenticado()
        {
            string rolUsuario = HttpContext.Session.GetString("UsuarioRol");
            return !string.IsNullOrEmpty(rolUsuario);
        }

        // ==========================================
        // SOLUCIÓN: ACCIÓN GET PARA RENDERIZAR LA VISTA
        // ==========================================
        // GET: Movimientos/RegistrarTransaccion
        [HttpGet]
        public IActionResult RegistrarTransaccion()
        {
            // 1. Control de acceso: Si no ha iniciado sesión, se le expulsa al Login
            if (!UsuarioEstaAutenticado())
            {
                TempData["Error"] = "Debe iniciar sesión para acceder a este módulo.";
                return RedirectToAction("Index", "Home");
            }

            // 2. Recuperar el catálogo de insumos activos para alimentar el <select> de la vista
            var listaInsumos = new List<Insumo>();
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            try
            {
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
                                listaInsumos.Add(new Insumo
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

                // 3. Almacenar la lista en el ViewBag para que esté disponible en el archivo .cshtml
                ViewBag.InsumosDisponibles = listaInsumos;
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar los insumos para la transacción: " + ex.Message;
                return RedirectToAction("Dashboard", "Home");
            }
        }

        // POST: Movimientos/RegistrarEgreso
        [HttpPost]
        public IActionResult RegistrarEgreso(int idInsumo, int idPersona, decimal cantidadARetirar, string tipoMovimiento)
        {
            // Control de acceso para peticiones POST
            if (!UsuarioEstaAutenticado())
            {
                return RedirectToAction("Index", "Home");
            }

            if (tipoMovimiento != "Consumo" && tipoMovimiento != "Merma" && tipoMovimiento != "Entrada")
            {
                TempData["Error"] = "Tipo de movimiento no válido.";
                return RedirectToAction("Dashboard", "Home");
            }

            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            using (conexion)
            {
                try
                {
                    conexion.Open();
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Error al conectar con la base de datos: " + ex.Message;
                    return RedirectToAction("Dashboard", "Home");
                }

                SqlTransaction transaccion = conexion.BeginTransaction();

                try
                {
                    decimal stockActual = 0;
                    string queryCheck = "SELECT CantidadActual FROM Insumo WHERE IdInsumo = @IdInsumo AND Activo = 1";

                    using (SqlCommand cmdCheck = new SqlCommand(queryCheck, conexion, transaccion))
                    {
                        cmdCheck.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        object resultado = cmdCheck.ExecuteScalar();

                        if (resultado == null)
                        {
                            throw new Exception("El insumo no existe o está inactivo.");
                        }
                        stockActual = Convert.ToDecimal(resultado);
                    }

                    string queryUpdate = "";

                    if (tipoMovimiento == "Entrada")
                    {
                        queryUpdate = "UPDATE Insumo SET CantidadActual = CantidadActual + @Cantidad WHERE IdInsumo = @IdInsumo";
                    }
                    else
                    {
                        if (cantidadARetirar > stockActual)
                        {
                            transaccion.Rollback();
                            TempData["Error"] = $"Operación denegada (RN-01): Intenta retirar {cantidadARetirar} unidades, pero solo hay {stockActual} en existencia.";
                            return RedirectToAction("Dashboard", "Home");
                        }

                        queryUpdate = "UPDATE Insumo SET CantidadActual = CantidadActual - @Cantidad WHERE IdInsumo = @IdInsumo";
                    }

                    using (SqlCommand cmdUpdate = new SqlCommand(queryUpdate, conexion, transaccion))
                    {
                        cmdUpdate.Parameters.AddWithValue("@Cantidad", cantidadARetirar);
                        cmdUpdate.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    string queryHistorial = @"INSERT INTO MovimientoInventario (IdInsumo, IdPersona, TipoMovimiento, Cantidad, FechaMovimiento) 
                                              VALUES (@IdInsumo, @IdPersona, @TipoMovimiento, @Cantidad, GETDATE())";
                    using (SqlCommand cmdHistorial = new SqlCommand(queryHistorial, conexion, transaccion))
                    {
                        cmdHistorial.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        cmdHistorial.Parameters.AddWithValue("@IdPersona", idPersona);
                        cmdHistorial.Parameters.AddWithValue("@TipoMovimiento", tipoMovimiento);
                        cmdHistorial.Parameters.AddWithValue("@Cantidad", cantidadARetirar);
                        cmdHistorial.ExecuteNonQuery();
                    }

                    transaccion.Commit();
                    TempData["Exito"] = $"Transacción de tipo '{tipoMovimiento}' registrada. Stock actualizado correctamente.";
                }
                catch (Exception ex)
                {
                    transaccion.Rollback();
                    TempData["Error"] = "Ocurrió un error en el servidor: " + ex.Message;
                }
            }

            // Redirección segura al panel de control principal
            return RedirectToAction("Dashboard", "Home");
        }
    }
}