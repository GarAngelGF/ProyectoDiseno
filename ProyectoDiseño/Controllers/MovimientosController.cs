using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;
using System;
using System.Collections.Generic; // Para listar insumos en el dropdown
using Microsoft.AspNetCore.Http; // Acceso a sesiones seguras

namespace ProyectoDiseño.Controllers
{
    public class MovimientosController : Controller
    {
        private bool UsuarioEstaAutenticado()
        {
            string rolUsuario = HttpContext.Session.GetString("UsuarioRol");
            return !string.IsNullOrEmpty(rolUsuario);
        }

        // ==========================================
        // CORRECCIÓN: ACCIÓN GET PARA RENDERIZAR LA VISTA DE TRANSACCIONES
        // ==========================================
        // GET: Movimientos/RegistrarTransaccion
        [HttpGet]
        public IActionResult RegistrarTransaccion()
        {
            if (!UsuarioEstaAutenticado())
            {
                TempData["Error"] = "Debe iniciar sesión para realizar movimientos de almacén.";
                return RedirectToAction("Index", "Home");
            }

            var listaInsumos = new List<Insumo>();
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            try
            {
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

                ViewBag.InsumosDisponibles = listaInsumos;
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error del servidor al inicializar el formulario: " + ex.Message;
                return RedirectToAction("Dashboard", "Home");
            }
        }

        // POST: Movimientos/RegistrarEgreso
        // CORRECCIÓN SEGURIDAD: Se elimina 'idPersona' de los parámetros para mitigar suplantación de identidad
        [HttpPost]
        public IActionResult RegistrarEgreso(int idInsumo, decimal cantidadARetirar, string tipoMovimiento)
        {
            if (!UsuarioEstaAutenticado())
            {
                return RedirectToAction("Index", "Home");
            }

            // CORRECCIÓN AUDITORÍA: Extracción estricta del ID del usuario desde la sesión en el Servidor
            int? idPersonaSegura = HttpContext.Session.GetInt32("UsuarioId");
            if (!idPersonaSegura.HasValue)
            {
                TempData["Error"] = "Sesión inválida. Vuelva a iniciar sesión.";
                return RedirectToAction("Index", "Home");
            }

            if (tipoMovimiento != "Consumo" && tipoMovimiento != "Merma" && tipoMovimiento != "Entrada")
            {
                TempData["Error"] = "Tipo de movimiento no válido.";
                return RedirectToAction("Dashboard", "Home");
            }

            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            try
            {
                decimal stockActual = 0;
                string queryCheck = "SELECT CantidadActual FROM Insumo WHERE IdInsumo = @IdInsumo AND Activo = 1";

                using (SqlCommand cmdCheck = new SqlCommand(queryCheck, conexion))
                {
                    cmdCheck.Parameters.AddWithValue("@IdInsumo", idInsumo);
                    object resultado = cmdCheck.ExecuteScalar();

                    if (resultado == null)
                    {
                        TempData["Error"] = "El insumo seleccionado no se encuentra activo.";
                        return RedirectToAction("Dashboard", "Home");
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
                        TempData["Error"] = $"Operación denegada (RN-01): Intenta retirar {cantidadARetirar} unidades, pero solo existen {stockActual}.";
                        return RedirectToAction("Dashboard", "Home");
                    }

                    queryUpdate = "UPDATE Insumo SET CantidadActual = CantidadActual - @Cantidad WHERE IdInsumo = @IdInsumo";
                }

                // Actualizar Stock
                using (SqlCommand cmdUpdate = new SqlCommand(queryUpdate, conexion))
                {
                    cmdUpdate.Parameters.AddWithValue("@Cantidad", cantidadARetirar);
                    cmdUpdate.Parameters.AddWithValue("@IdInsumo", idInsumo);
                    cmdUpdate.ExecuteNonQuery();
                }

                // Insertar Historial con la Identidad Verificada de la Sesión
                string queryHistorial = @"INSERT INTO MovimientoInventario (IdInsumo, IdPersona, TipoMovimiento, Cantidad, FechaMovimiento) 
                                          VALUES (@IdInsumo, @IdPersona, @TipoMovimiento, @Cantidad, GETDATE())";
                using (SqlCommand cmdHistorial = new SqlCommand(queryHistorial, conexion))
                {
                    cmdHistorial.Parameters.AddWithValue("@IdInsumo", idInsumo);
                    cmdHistorial.Parameters.AddWithValue("@IdPersona", idPersonaSegura.Value);
                    cmdHistorial.Parameters.AddWithValue("@TipoMovimiento", tipoMovimiento);
                    cmdHistorial.Parameters.AddWithValue("@Cantidad", cantidadARetirar);
                    cmdHistorial.ExecuteNonQuery();
                }

                TempData["Exito"] = $"Transacción '{tipoMovimiento}' completada con éxito.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error transaccional en el servidor: " + ex.Message;
            }

            // CORRECCIÓN ENRUTAMIENTO: Redirección segura a Home/Dashboard (Solución de Error 404)
            return RedirectToAction("Dashboard", "Home");
        }
    }
}