using System;

namespace ProyectoDiseño.Models
{
    public class MovimientoInventario
    {
        public int IdMovimiento { get; set; }
        public int IdInsumo { get; set; }
        public int IdPersona { get; set; }
        public string TipoMovimiento { get; set; }
        public decimal Cantidad { get; set; }
        public DateTime FechaMovimiento { get; set; }
        public string Observaciones { get; set; }

        // ==========================================
        // PROPIEDADES DE APOYO PARA LAS VISTAS (JOINS)
        // ==========================================
        public string NombreInsumo { get; set; }
        public string NombrePersona { get; set; }
    }
}