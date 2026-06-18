using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;
using System;
using Microsoft.AspNetCore.Http;

namespace ProyectoDiseño.Controllers
{
    public class MovimientosController : Controller
    {
        // POST: Movimientos/RegistrarEgreso
        [HttpPost]
        public IActionResult RegistrarEgreso(int idInsumo, int idPersona, decimal cantidadARetirar, string tipoMovimiento)
        {
            // Validar que el tipo de movimiento corresponda a las reglas del negocio (RF-02)
            if (tipoMovimiento != "Consumo" && tipoMovimiento != "Merma" && tipoMovimiento != "Entrada")
            {
                TempData["Error"] = "Tipo de movimiento no válido.";
                return RedirectToAction("Dashboard", "Home");
            }

            // Si el ID de la persona viene en 0 o vacío por alguna desincronización del cliente,
            // intentamos recuperarlo directamente de la sesión activa en el servidor
            if (idPersona <= 0)
            {
                idPersona = HttpContext.Session.GetInt32("UsuarioId") ?? 0;
                if (idPersona == 0)
                {
                    TempData["Error"] = "Error de autenticación: No se pudo identificar al usuario que registra el movimiento.";
                    return RedirectToAction("Index", "Home");
                }
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
                    TempData["Error"] = "Error de conexión con el servidor de datos: " + ex.Message;
                    return RedirectToAction("Dashboard", "Home");
                }

                // Iniciamos una transacción SQL explícita para asegurar la consistencia atómica (RNF-01)
                SqlTransaction transaccion = conexion.BeginTransaction();

                try
                {
                    // 1. Consultar el estado y stock físico actual del insumo en el almacén
                    decimal stockActual = 0;
                    string queryCheck = "SELECT CantidadActual FROM Insumo WHERE IdInsumo = @IdInsumo AND Activo = 1";

                    using (SqlCommand cmdCheck = new SqlCommand(queryCheck, conexion, transaccion))
                    {
                        cmdCheck.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        object resultado = cmdCheck.ExecuteScalar();

                        if (resultado == null)
                        {
                            throw new Exception("El insumo seleccionado no existe en el catálogo o se encuentra inactivo.");
                        }
                        stockActual = Convert.ToDecimal(resultado);
                    }

                    // 2. Determinar el sentido aritmético de la operación e interceptar flujos inválidos
                    string queryUpdate = "";

                    if (tipoMovimiento == "Entrada")
                    {
                        // Incremento de stock por concepto de compras de mercancía
                        queryUpdate = "UPDATE Insumo SET CantidadActual = CantidadActual + @Cantidad, FechaUltimaActualizacion = GETDATE() WHERE IdInsumo = @IdInsumo";
                    }
                    else
                    {
                        // Validación estricta de la Regla de Negocio RN-01 (Prevenir inventario negativo)
                        if (cantidadARetirar > stockActual)
                        {
                            transaccion.Rollback();
                            TempData["Error"] = $"Operación denegada (RN-01): Intenta retirar {cantidadARetirar} unidades de un stock disponible de {stockActual}.";
                            return RedirectToAction("Dashboard", "Home");
                        }

                        // Decremento de stock por consumo operativo o pérdidas por merma
                        queryUpdate = "UPDATE Insumo SET CantidadActual = CantidadActual - @Cantidad, FechaUltimaActualizacion = GETDATE() WHERE IdInsumo = @IdInsumo";
                    }

                    // 3. Ejecutar la actualización directa sobre el registro de existencias
                    using (SqlCommand cmdUpdate = new SqlCommand(queryUpdate, conexion, transaccion))
                    {
                        cmdUpdate.Parameters.AddWithValue("@Cantidad", cantidadARetirar);
                        cmdUpdate.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        cmdUpdate.ExecuteNonQuery(); // Ejecución directa en ADO.NET para garantizar respuesta < 2 segundos
                    }

                    // 4. Persistir la traza de auditoría en el historial transaccional enlazando a la Persona
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

                    // Confirmación definitiva si todas las instrucciones previas se ejecutaron con éxito
                    transaccion.Commit();
                    TempData["Exito"] = $"Movimiento de tipo '{tipoMovimiento}' procesado y stock actualizado correctamente.";
                }
                catch (Exception ex)
                {
                    // Ante cualquier excepción imprevista, se deshacen las operaciones intermedias automáticamente
                    transaccion.Rollback();
                    TempData["Error"] = "Error interno en el procesamiento del movimiento: " + ex.Message;
                }
            }

            // Redirección segura al panel unificado controlado por la sesión del Home
            return RedirectToAction("Dashboard", "Home");
        }
    }
}