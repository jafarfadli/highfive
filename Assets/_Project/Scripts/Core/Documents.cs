using System.Collections.Generic;
using UnityEngine;

namespace SeoulFirstWeek.Core
{
    /// <summary>Documents the player can carry and hand over at a counter.</summary>
    public static class Documents
    {
        public class Info
        {
            public string Id;
            public string Korean;
            public string[] Names; // en, vi, id
            public Color Color;
            public string Glyph;
        }

        public static readonly string[] StartingInventory = { "passport", "enrollment", "photo", "card", "cash" };

        static readonly List<Info> All = new()
        {
            Make("passport", "여권", new[] { "Passport", "Hộ chiếu", "Paspor" }, "#2F4A8A", "P"),
            Make("enrollment", "재학증명서", new[] { "Certificate of enrollment", "Giấy chứng nhận đang học", "Surat keterangan kuliah" }, "#6B5B95", "학"),
            Make("photo", "증명사진", new[] { "ID photo", "Ảnh thẻ", "Pas foto" }, "#88A0A8", "사"),
            Make("card", "해외 카드", new[] { "Overseas debit card", "Thẻ ngân hàng nước ngoài", "Kartu debit luar negeri" }, "#C4553B", "카"),
            Make("cash", "현금", new[] { "Cash (KRW)", "Tiền mặt (KRW)", "Uang tunai (KRW)" }, "#4E8D5B", "₩"),
            Make("arc_form", "통합신청서", new[] { "Application form", "Đơn đăng ký", "Formulir permohonan" }, "#B08D3C", "신"),
            Make("arc_receipt", "접수증", new[] { "ARC application receipt", "Giấy biên nhận hồ sơ", "Tanda terima permohonan" }, "#7A7A7A", "접"),
            Make("arc", "외국인등록증", new[] { "Alien Registration Card", "Thẻ đăng ký người nước ngoài", "Kartu Registrasi Orang Asing" }, "#2C8C8C", "등"),
            Make("prescription", "처방전", new[] { "Prescription", "Đơn thuốc", "Resep" }, "#D96C8A", "처"),
            Make("bankbook", "통장", new[] { "Bankbook", "Sổ ngân hàng", "Buku tabungan" }, "#3B8F4F", "통"),
            Make("checkcard", "체크카드", new[] { "Korean debit card", "Thẻ ghi nợ Hàn Quốc", "Kartu debit Korea" }, "#1F7A3A", "체"),
            Make("medcert", "진단서", new[] { "Medical certificate", "Giấy chẩn đoán", "Surat keterangan dokter" }, "#C0564B", "진"),
            Make("attendance", "출석증명서", new[] { "Attendance certificate", "Giấy chứng nhận chuyên cần", "Surat keterangan kehadiran" }, "#5E60CE", "출"),
            Make("tuition", "등록금 납입증명서", new[] { "Tuition payment certificate", "Giấy xác nhận đã đóng học phí", "Bukti pembayaran uang kuliah" }, "#8E6C8A", "등"),
            Make("ext_receipt", "연장 접수증", new[] { "Extension receipt", "Giấy biên nhận gia hạn", "Tanda terima perpanjangan" }, "#6C7A89", "연"),
            Make("remit_info", "송금 정보 메모", new[] { "Recipient details (note)", "Ghi chú thông tin người nhận", "Catatan data penerima" }, "#E0A93B", "메"),
            Make("remit_receipt", "송금 영수증", new[] { "Remittance receipt", "Biên lai chuyển tiền", "Bukti pengiriman uang" }, "#2F7F7A", "송"),
            Make("dorm_cert", "기숙사 입사확인서", new[] { "Dormitory residence certificate", "Giấy xác nhận ở ký túc xá", "Surat keterangan tinggal di asrama" }, "#6C8EBF", "기"),
            Make("tcard", "교통카드", new[] { "Transport card", "Thẻ giao thông", "Kartu transportasi" }, "#00A4E3", "교"),
            Make("bus_ticket", "공항버스 승차권", new[] { "Airport bus ticket", "Vé xe buýt sân bay", "Tiket bus bandara" }, "#2A6FDB", "표"),
            Make("er_form", "진료신청서", new[] { "ER registration form", "Phiếu đăng ký khám", "Formulir pendaftaran UGD" }, "#B08D3C", "신"),
            Make("er_receipt", "진료비 영수증", new[] { "Medical bill receipt", "Biên lai viện phí", "Kuitansi biaya berobat" }, "#7A7A7A", "영"),
        };

        static readonly Dictionary<string, Info> ById = new();

        static Documents()
        {
            foreach (var info in All) ById[info.Id] = info;
        }

        static Info Make(string id, string ko, string[] names, string hex, string glyph)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return new Info { Id = id, Korean = ko, Names = names, Color = color, Glyph = glyph };
        }

        public static Info Get(string id) => ById.TryGetValue(id, out var info) ? info : null;

        public static string Korean(string id) => Get(id)?.Korean ?? id;

        public static string LocalName(string id)
        {
            var info = Get(id);
            if (info == null) return id;
            var lang = (int)Settings.Language;
            return info.Names[lang];
        }

        // shorter names where room is tight (the note on a part card that needs the document)
        static readonly Dictionary<string, string[]> Shorts = new()
        {
            ["arc"] = new[] { "a residence card (ARC)", "thẻ cư trú (ARC)", "kartu tinggal (ARC)" },
            ["bankbook"] = new[] { "a bankbook", "sổ ngân hàng", "buku tabungan" },
        };

        /// <summary>A short name in the player's language, for tight spots such as the note on a part card.</summary>
        public static string ShortName(string id) =>
            Shorts.TryGetValue(id, out var names) ? names[(int)Settings.Language] : LocalName(id);

        public static string Flag(string id) => "doc:" + id;
    }
}
