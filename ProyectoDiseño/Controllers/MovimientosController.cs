using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;
using System;
using Microsoft.AspNetCore.Http; // IMPORTANTE: Necesario para el manejo de sesiones

namespace ProyectoDiseño.Controllers
{
    public class MovimientosController : Controller
    {
        // Método privado auxiliar para verificar que existe una sesión activa
        private bool UsuarioEstaAutenticado()
        {
            string rolUsuario = HttpContext.Session.GetString("UsuarioRol");
            return !string.IsNullOrEmpty(rolUsuario);
        }

        // POST: Movimientos/RegistrarEgreso
        [HttpPost]
        public IActionResult RegistrarEgreso(int idInsumo, int idPersona, decimal cantidadARetirar, string tipoMovimiento)
        {
            // SOLUCIÓN PUNTO 1: Validación de acceso para evitar operaciones de usuarios anónimos
            if (!UsuarioEstaAutenticado())
            {
                return RedirectToAction("Index", "Home"); // Expulsar al login si no hay sesión
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

            // CORRECCIÓN ADICIONAL: Se corrige el Error 404 apuntando correctamente al controlador "Home"
            return RedirectToAction("Dashboard", "Home");
        }
    }
}