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
            // Validamos que el tipo de movimiento sea correcto para un egreso
            if (tipoMovimiento != "Consumo" && tipoMovimiento != "Merma")
            {
                TempData["Error"] = "Tipo de movimiento no válido.";
                return RedirectToAction("Index");
            }

            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            using (conexion)
            {
                conexion.Open();

                // Iniciamos una transacción SQL para asegurar la integridad de los datos de stock
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
                        // Se interrumpe el flujo y no se realiza la operación 
                        transaccion.Rollback();
                        TempData["Error"] = $"Operación denegada: Intenta retirar {cantidadARetirar} pero solo hay {stockActual} en existencia.";
                        return RedirectToAction("Index"); // Devuelve a la vista con el mensaje de error
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
                        cmdHistorial.Parameters.AddWithValue("@IdPersona", idPersona); // El ID del Administrador o Personal de Cocina
                        cmdHistorial.Parameters.AddWithValue("@TipoMovimiento", tipoMovimiento);
                        cmdHistorial.Parameters.AddWithValue("@Cantidad", cantidadARetirar);
                        cmdHistorial.ExecuteNonQuery();
                    }

                    // Confirmamos la transacción
                    transaccion.Commit();
                    TempData["Exito"] = "Movimiento registrado y stock actualizado correctamente.";
                }
                catch (Exception ex)
                {
                    transaccion.Rollback();
                    TempData["Error"] = "Ocurrió un error en el servidor: " + ex.Message;
                }
            }

            return RedirectToAction("Index"); // Redirige al panel de cocina o inventario
        }
    }
}
