using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;
using System;
using Microsoft.AspNetCore.Http; // Para acceder a las sesiones

namespace ProyectoDiseño.Controllers
{
    public class MovimientosController : Controller
    {
        // POST: Movimientos/RegistrarEgreso
        [HttpPost]
        public IActionResult RegistrarEgreso(int idInsumo, int idPersona, decimal cantidadARetirar, string tipoMovimiento)
        {
            // 1. CORRECCIÓN: Se agrega "Entrada" como un tipo de movimiento válido
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
                    // Obtener la cantidad actual del insumo
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

                    // 2. CORRECCIÓN: Lógica matemática dinámica según el tipo de movimiento
                    if (tipoMovimiento == "Entrada")
                    {
                        // Si es una compra, se SUMA al inventario y NO se aplica la validación de negativos
                        queryUpdate = "UPDATE Insumo SET CantidadActual = CantidadActual + @Cantidad WHERE IdInsumo = @IdInsumo";
                    }
                    else
                    {
                        // Si es Consumo o Merma, validamos que no deje el inventario en negativo (RN-01)
                        if (cantidadARetirar > stockActual)
                        {
                            transaccion.Rollback();
                            TempData["Error"] = $"Operación denegada (RN-01): Intenta retirar {cantidadARetirar} unidades, pero solo hay {stockActual} en existencia.";
                            return RedirectToAction("Dashboard", "Home");
                        }

                        // Se RESTA del inventario
                        queryUpdate = "UPDATE Insumo SET CantidadActual = CantidadActual - @Cantidad WHERE IdInsumo = @IdInsumo";
                    }

                    // 3. Ejecutar la actualización del stock
                    using (SqlCommand cmdUpdate = new SqlCommand(queryUpdate, conexion, transaccion))
                    {
                        cmdUpdate.Parameters.AddWithValue("@Cantidad", cantidadARetirar);
                        cmdUpdate.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    // 4. Registrar el historial de auditoría asociando a la Persona
                    string queryHistorial = @"INSERT INTO MovimientoInventario (IdInsumo, IdPersona, TipoMovimiento, Cantidad, FechaMovimiento) 
                                              VALUES (@IdInsumo, @IdPersona, @TipoMovimiento, @Cantidad, GETDATE())";
                    using (SqlCommand cmdHistorial = new SqlCommand(queryHistorial, conexion, transaccion))
                    {
                        cmdHistorial.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        cmdHistorial.Parameters.AddWithValue("@IdPersona", idPersona);
                        cmdHistorial.Parameters.AddWithValue("@TipoMovimiento", tipoMovimiento);
                        cmdHistorial.Parameters.AddWithValue("@Cantidad", cantidadARetirar); // Guarda cuánto se operó
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

            // CORRECCIÓN FINAL: Redirige de forma segura a la acción Dashboard del HomeController
            // (Ya no redirige a Index porque Index ahora es la pantalla de Login)
            return RedirectToAction("Dashboard", "Index");
        }
    }
}