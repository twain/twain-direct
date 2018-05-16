using System.ComponentModel.DataAnnotations;

namespace TwainDirect.Scanner.Storage
{
    public class CloudScanner
    {
        [Key]
        public string Id { get; set; }

        public string Name { get; set; }

        public string AuthorizationToken { get; set; }

        public string RefreshToken { get; set; }

        public override string ToString()
        {
            return $"{Name} - {Id}";
        }
    }
}
