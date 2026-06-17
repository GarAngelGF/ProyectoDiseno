namespace ProyectoDiseño.Models
{
    public interface IInsumoPrototype
    {
        Insumo Clonar(string nuevoNombre, decimal nuevaCantidad);
    }
    public class Insumo: IInsumoPrototype
    {
        public int IdInsumo { get; set; }
        public string Nombre { get; set; }
        public string UnidadMedida { get; set; }
        public decimal CantidadActual { get; set; }
        public decimal StockMinimo { get; set; }
        public bool Activo { get; set; }

        // Implementación del método de clonación
        public Insumo Clonar(string nuevoNombre, decimal nuevaCantidad)
        {
            // Crea una copia superficial (Shallow copy) del objeto actual
            Insumo insumoClonado = (Insumo)this.MemberwiseClone();

            // Modificamos únicamente los campos específicos solicitados
            insumoClonado.Nombre = nuevoNombre;
            insumoClonado.CantidadActual = nuevaCantidad;

            // Reiniciamos el ID ya que será un nuevo registro en la base de datos
            insumoClonado.IdInsumo = 0;

            return insumoClonado;
        }
    }
}
