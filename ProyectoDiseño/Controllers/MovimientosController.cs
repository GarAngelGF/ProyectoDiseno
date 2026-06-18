using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http; // REQUERIDO: Para acceder a HttpContext.Session

namespace ProyectoDiseño.Controllers
{
    public class MovimientosController : Controller
    {
        private bool UsuarioEstaAutenticado()
        {
            string rolUsuario = HttpContext.Session.GetString("UsuarioRol");
            return !string.IsNullOrEmpty(rolUsuario);
        }

        // GET: Movimientos/RegistrarTransaccion
        [HttpGet]
        public IActionResult RegistrarTransaccion()
        {
            if (!UsuarioEstaAutenticado())
            {
                TempData["Error"] = "Debe iniciar sesión para acceder a este módulo.";
                return RedirectToAction("Index", "Home");
            }

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

                ViewBag.InsumosDisponibles = listaInsumos;
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar los insumos: " + ex.Message;
                return RedirectToAction("Dashboard", "Home");
            }
        }

        // POST: Movimientos/RegistrarEgreso
        // SOLUCIÓN: Se eliminó 'int idPersona' de los parámetros de la solicitud externa
        [HttpPost]
        public IActionResult RegistrarEgreso(int idInsumo, decimal cantidadARetirar, string tipoMovimiento)
        {
            // 1. CONTROL DE ACCESO GLOBAL
            if (!UsuarioEstaAutenticado())
            {
                return RedirectToAction("Index", "Home");
            }

            // =========================================================================
            // 2. SOLUCIÓN: EXTRACCIÓN SEGURA DE LA IDENTIDAD DEL LADO DEL SERVIDOR
            // =========================================================================
            int? idPersonaLogueada = HttpContext.Session.GetInt32("UsuarioId");
            
            if (!idPersonaLogueada.HasValue)
            {
                TempData["Error"] = "Su sesión ha expirado o es inválida. Por favor, vuelva a autenticarse.";
                return RedirectToAction("Index", "Home");
            }

            // Convertimos el valor seguro a tipo int para su uso en las consultas SQL
            int idPersonaSegura = idPersonaLogueada.Value;
            // =========================================================================

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

                    // 3. REGISTRO EN EL HISTORIAL USANDO LA VARIABLE DE SESIÓN SEGURA
                    string queryHistorial = @"INSERT INTO MovimientoInventario (IdInsumo, IdPersona, TipoMovimiento, Cantidad, FechaMovimiento) 
                                              VALUES (@IdInsumo, @IdPersona, @TipoMovimiento, @Cantidad, GETDATE())";
                    using (SqlCommand cmdHistorial = new SqlCommand(queryHistorial, conexion, transaccion))
                    {
                        cmdHistorial.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        cmdHistorial.Parameters.AddWithValue("@IdPersona", idPersonaSegura); // Asignación del valor de sesión validado
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

            return RedirectToAction("Dashboard", "Home");
        }
    }
}