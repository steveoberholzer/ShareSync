namespace Tecala.SMO.ShareSync.Models
{
    internal class CsvInteractionRow
    {
        public string InteractionId { get; set; }
        public string ProjectSubfolder { get; set; }
        public string InternalPermission { get; set; }
        public string InternalUserEmails { get; set; }
        public string ExternalPermission { get; set; }
        public string ExternalUserEmails { get; set; }
    }
}
