using ProyectoDiseño.Models;
using System.Collections.Generic;

namespace ProyectoDiseño.ViewModels
{
    public class DashboardViewModel
    {
        public List<Insumo> InventarioCompleto { get; set; }
        public List<Insumo> AlertasStock { get; set; }
    }
}
