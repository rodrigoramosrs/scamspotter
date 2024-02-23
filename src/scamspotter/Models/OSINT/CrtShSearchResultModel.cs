namespace ScamSpotter.Models.OSINT
{
    public class CrtShSearchResultModel
    {
        public int issuer_ca_id { get; set; }
        public string issuer_name { get; set; }
        public string common_name { get; set; }
        public string name_value { get; set; }
        public long id { get; set; }
        public DateTime entry_timestamp { get; set; }
        public DateTime not_before { get; set; }
        public DateTime not_after { get; set; }
        public string serial_number { get; set; }
        public int result_count { get; set; }


    }
}
