using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;
using System;

namespace ProyectoDiseño.Controllers
{
    public class MovimientosController : Controller
    {
        // POST: Movimientos/RegistrarEgreso
        [HttpPost]
        public IActionResult RegistrarEgreso(int idInsumo, int idPersona, decimal cantidadARetirar, string tipoMovimiento)
        {
            // Determinamos el rol dinámicamente según el flujo para la redirección final
            string rolRetorno = (tipoMovimiento == "Consumo") ? "Cocina" : "Administrador";

            if (tipoMovimiento != "Consumo" && tipoMovimiento != "Merma")
            {
                TempData["Error"] = "Tipo de movimiento no válido.";
                // CORREGIDO: Redirige al Dashboard de la Home
                return RedirectToAction("Index", "Home", new { rolUsuario = rolRetorno });
            }

            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            using (conexion)
            {
                conexion.Open();
                SqlTransaction transaccion = conexion.BeginTransaction();

                try
                {
                    // 1. Obtener la cantidad actual del insumo
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

                    // 2. Validación de la Regla de Negocio RN-01 (Interrupción de flujo)
                    if (cantidadARetirar > stockActual)
                    {
                        transaccion.Rollback();
                        TempData["Error"] = $"Operación denegada: Intenta retirar {cantidadARetirar} pero solo hay {stockActual} en existencia.";
                        // CORREGIDO: Redirige al Dashboard correspondiente conservando el mensaje de error
                        return RedirectToAction("Index", "Home", new { rolUsuario = rolRetorno });
                    }

                    // 3. Descontar las unidades del inventario
                    string queryUpdate = "UPDATE Insumo SET CantidadActual = CantidadActual - @Cantidad WHERE IdInsumo = @IdInsumo";
                    using (SqlCommand cmdUpdate = new SqlCommand(queryUpdate, conexion, transaccion))
                    {
                        cmdUpdate.Parameters.AddWithValue("@Cantidad", cantidadARetirar);
                        cmdUpdate.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    // 4. Registrar el historial asociando a la clase Persona
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
                    TempData["Exito"] = "Movimiento registrado y stock actualizado correctamente.";
                }
                catch (Exception ex)
                {
                    transaccion.Rollback();
                    TempData["Error"] = "Ocurrió un error en el servidor: " + ex.Message;
                }
            }

            // CORREGIDO: Regresa de forma segura al panel del usuario con los resultados actualizados
            return RedirectToAction("Index", "Home", new { rolUsuario = rolRetorno });
        }
    }
}