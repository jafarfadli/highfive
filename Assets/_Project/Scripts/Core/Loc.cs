using System.Collections.Generic;

namespace SeoulFirstWeek.Core
{
    public enum HintLanguage { English, Vietnamese, Indonesian }

    /// <summary>UI strings in the three player languages (en / vi / id).</summary>
    public static class Loc
    {
        public static string Code(HintLanguage lang) => lang switch
        {
            HintLanguage.Vietnamese => "vi",
            HintLanguage.Indonesian => "id",
            _ => "en",
        };

        public static string LanguageName(HintLanguage lang) => lang switch
        {
            HintLanguage.Vietnamese => "Tiếng Việt",
            HintLanguage.Indonesian => "Bahasa Indonesia",
            _ => "English",
        };

        public static string T(string key, params object[] args)
        {
            if (!Table.TryGetValue(key, out var row)) return key;
            var text = row[(int)Settings.Language];
            if (string.IsNullOrEmpty(text)) text = row[0];
            return args.Length > 0 ? string.Format(text, args) : text;
        }

        // key -> { en, vi, id }
        static readonly Dictionary<string, string[]> Table = new()
        {
            ["choose_lang"] = new[] { "Your language", "Ngôn ngữ của bạn", "Bahasamu" },
            ["tagline"] = new[] { "Rehearse your first week in Seoul, one counter at a time.", "Tập dượt tuần đầu ở Seoul, từng quầy một.", "Latihan minggu pertamamu di Seoul, satu loket demi satu." },
            ["home_nurse"] = new[] { "What seems to be the problem?", "Bạn thấy không khỏe ở đâu?", "Apa keluhannya?" },
            ["language"] = new[] { "Language", "Ngôn ngữ", "Bahasa" },
            ["hear_again"] = new[] { "hear again", "nghe lại", "dengar lagi" },
            ["reduced_motion"] = new[] { "Calm animation", "Giảm hiệu ứng động", "Kurangi animasi" },
            ["on"] = new[] { "On", "Bật", "Aktif" },
            ["off"] = new[] { "Off", "Tắt", "Mati" },
            ["play"] = new[] { "play", "chơi", "main" },
            ["click_to_start"] = new[] { "Click anywhere to start", "Nhấp vào bất kỳ đâu để bắt đầu", "Klik di mana saja untuk mulai" },
            ["choose_scenario"] = new[] { "Choose a scenario", "Chọn tình huống", "Pilih skenario" },
            ["part_n"] = new[] { "Part {0}", "Phần {0}", "Bagian {0}" },
            ["obj_wait_n"] = new[] { "Wait for number {0}", "Chờ đến số {0}", "Tunggu nomor {0}" },
            ["obj_wait_name"] = new[] { "Have a seat and wait for your name", "Ngồi chờ đến khi được gọi tên", "Duduk dan tunggu namamu dipanggil" },
            ["levels_done"] = new[] { "{0} / {1} done", "Xong {0} / {1}", "{0} / {1} selesai" },
            ["coming_soon"] = new[] { "Coming soon", "Sắp ra mắt", "Segera hadir" },
            ["needs_doc"] = new[] { "Needs {0}", "Cần {0}", "Butuh {0}" },
            ["done_badge"] = new[] { "Done", "Xong", "Selesai" },
            ["back"] = new[] { "Back", "Quay lại", "Kembali" },
            ["reset_progress"] = new[] { "Reset progress", "Xóa tiến độ", "Hapus progres" },
            ["reset_progress_confirm"] = new[] { "Erase all progress and documents?", "Xóa toàn bộ tiến độ và giấy tờ?", "Hapus semua progres dan dokumen?" },
            ["controls"] = new[] { "Hold left mouse + drag: look · WASD: move · E / click: interact · Esc: menu", "Giữ chuột trái + kéo: nhìn · WASD: đi · E / nhấp: tương tác · Esc: menu", "Tahan klik kiri + geser: lihat · WASD: jalan · E / klik: interaksi · Esc: menu" },
            ["go_to"] = new[] { "Go to {0}", "Đến {0}", "Pergi ke {0}" },
            ["enter"] = new[] { "Enter {0}", "Vào {0}", "Masuk {0}" },
            ["take_ticket"] = new[] { "Take a queue number (번호표)", "Lấy số thứ tự (번호표)", "Ambil nomor antrean (번호표)" },
            ["obj_go_counter"] = new[] { "Go to counter {0}", "Đến quầy {0}", "Ke loket {0}" },
            ["talk_to"] = new[] { "Talk to {0}", "Nói chuyện với {0}", "Bicara dengan {0}" },
            ["talk_to_role"] = new[] { "Talk to the {0}", "Nói chuyện với {0}", "Bicara dengan {0}" },
            ["counter_n"] = new[] { "counter {0}", "quầy {0}", "loket {0}" },
            ["now_serving"] = new[] { "Now serving", "Đang phục vụ", "Sedang dilayani" },
            ["people_ahead"] = new[] { "{0} ahead", "{0} người trước", "{0} di depan" },
            ["your_turn"] = new[] { "It's your turn!", "Đến lượt bạn!", "Giliranmu!" },
            ["replay"] = new[] { "R  Hear again", "R  Nghe lại", "R  Dengar lagi" },
            ["try_another"] = new[] { "That doesn't fit. Try another answer.", "Chưa đúng. Thử câu khác.", "Kurang pas. Coba jawaban lain." },
            ["pin_mismatch"] = new[] { "The PINs don't match. Enter the same 4 digits again.", "PIN không khớp. Nhập lại đúng 4 số.", "PIN tidak cocok. Masukkan 4 angka yang sama lagi." },
            ["keypad_title"] = new[] { "Enter a 4-digit PIN", "Nhập PIN 4 số", "Masukkan PIN 4 digit" },
            ["fill_form"] = new[] { "Fill in the form", "Điền đơn", "Isi formulir" },
            ["return_counter"] = new[] { "Back to counter {0}", "Quay lại quầy {0}", "Kembali ke loket {0}" },
            ["form_instruction"] = new[] { "Tick what you are applying for.", "Đánh dấu việc bạn đăng ký.", "Centang yang kamu ajukan." },
            ["form_wrong"] = new[] { "Wrong box. Read the Korean again.", "Sai ô. Đọc lại tiếng Hàn.", "Kotak salah. Baca lagi tulisan Koreanya." },
            ["form_done"] = new[] { "Form done. Back to the counter.", "Xong đơn. Quay lại quầy.", "Formulir selesai. Kembali ke loket." },
            ["received"] = new[] { "Received: {0}", "Đã nhận: {0}", "Diterima: {0}" },
            ["brought"] = new[] { "You brought", "Bạn mang theo", "Kamu membawa" },
            ["leave_building"] = new[] { "Leave building", "Rời khỏi tòa nhà", "Keluar gedung" },
            ["leave_confirm"] = new[] { "Leave now? This level ends here.", "Rời đi? Màn chơi sẽ kết thúc.", "Keluar sekarang? Level ini berakhir." },
            ["yes"] = new[] { "Yes", "Có", "Ya" },
            ["no"] = new[] { "No", "Không", "Tidak" },
            ["pause_title"] = new[] { "Paused", "Tạm dừng", "Jeda" },
            ["resume"] = new[] { "Resume", "Tiếp tục", "Lanjutkan" },
            ["settings"] = new[] { "Settings", "Cài đặt", "Pengaturan" },
            ["reset"] = new[] { "Reset", "Chơi lại", "Ulang" },
            ["reset_confirm"] = new[] { "Restart this level from the beginning?", "Chơi lại màn này từ đầu?", "Ulang level ini dari awal?" },
            ["main_menu"] = new[] { "Main menu", "Menu chính", "Menu utama" },
            ["menu_confirm"] = new[] { "Back to the main menu? This level's progress is lost.", "Về menu chính? Tiến độ màn này sẽ mất.", "Kembali ke menu utama? Progres level ini hilang." },
            ["music"] = new[] { "Music", "Nhạc", "Musik" },
            ["sfx"] = new[] { "SFX", "Hiệu ứng", "SFX" },
            ["sensitivity"] = new[] { "Sensitivity", "Độ nhạy", "Sensitivitas" },
            ["progress"] = new[] { "Progress", "Tiến độ", "Progres" },
            ["outcome_completed"] = new[] { "Level complete!", "Hoàn thành!", "Level selesai!" },
            ["outcome_blocked"] = new[] { "Turned away: lesson learned", "Bị từ chối: đã rút ra bài học", "Ditolak: pelajaran didapat" },
            ["outcome_abandoned"] = new[] { "Left early", "Rời đi sớm", "Pergi lebih awal" },
            ["mistakes"] = new[] { "Wrong answers", "Câu sai", "Jawaban salah" },
            ["time"] = new[] { "Time", "Thời gian", "Waktu" },
            ["tips"] = new[] { "Remember", "Ghi nhớ", "Ingat" },
            ["no_mistakes"] = new[] { "No wrong answers. Nice!", "Không câu nào sai. Giỏi lắm!", "Tidak ada jawaban salah. Mantap!" },
            ["phrase_card"] = new[] { "Phrase card", "Thẻ mẫu câu", "Kartu frasa" },
            ["save_card"] = new[] { "Save phrase card", "Lưu thẻ mẫu câu", "Simpan kartu frasa" },
            ["logbook"] = new[] { "Logbook", "Sổ tay", "Logbook" },
            ["logbook_sub"] = new[] { "Look back at a finished part before the real visit: papers, words and the whole conversation.", "Xem lại phần đã hoàn thành trước khi đi thật: giấy tờ, từ vựng và cả cuộc hội thoại.", "Cek lagi bagian yang sudah selesai sebelum datang sungguhan: dokumen, kosakata, dan seluruh percakapan." },
            ["logbook_locked"] = new[] { "Finish this part to unlock", "Hoàn thành phần này để mở", "Selesaikan bagian ini untuk membuka" },
            ["logbook_empty_title"] = new[] { "Nothing here yet", "Chưa có gì ở đây", "Belum ada isinya" },
            ["logbook_empty"] = new[] { "Finish a part and its papers, words and conversation are kept here.", "Hoàn thành một phần, giấy tờ, từ vựng và hội thoại sẽ được lưu ở đây.", "Selesaikan satu bagian, dokumen, kosakata, dan percakapannya tersimpan di sini." },
            ["what_to_bring"] = new[] { "What to bring", "Cần mang theo", "Yang perlu dibawa" },
            ["you_get"] = new[] { "You leave with", "Bạn nhận được", "Yang kamu bawa pulang" },
            ["key_words"] = new[] { "Key words", "Từ khóa", "Kosakata penting" },
            ["tab_brief"] = new[] { "Before you go", "Trước khi đi", "Sebelum berangkat" },
            ["tab_talk"] = new[] { "Conversation", "Hội thoại", "Percakapan" },
            ["announcement"] = new[] { "Announcement", "Thông báo", "Pengumuman" },
            ["hand_over"] = new[] { "Hand over: {0}", "Đưa: {0}", "Serahkan: {0}" },
            ["enter_pin"] = new[] { "Enter a 4-digit PIN on the keypad", "Nhập mã PIN 4 số trên bàn phím", "Masukkan PIN 4 digit di keypad" },
            ["enter_pin_again"] = new[] { "Enter the same PIN again", "Nhập lại đúng mã PIN đó", "Masukkan PIN yang sama sekali lagi" },
            ["pay_card_cash"] = new[] { "Card or cash", "Thẻ hoặc tiền mặt", "Kartu atau tunai" },
            ["or"] = new[] { "or", "hoặc", "atau" },
            ["filled_there"] = new[] { "filled in there", "điền tại chỗ", "diisi di sana" },
            ["saved_to"] = new[] { "Saved to {0}", "Đã lưu vào {0}", "Disimpan di {0}" },
            ["retry"] = new[] { "Retry", "Chơi lại", "Ulangi" },
            ["levels"] = new[] { "Scenarios", "Tình huống", "Skenario" },
            ["arc_arrived"] = new[] { "(arrives about 3 weeks later)", "(đến sau khoảng 3 tuần)", "(tiba sekitar 3 minggu kemudian)" },
            ["atm_prompt"] = new[] { "ATM (자동화기기)", "ATM (자동화기기)", "ATM (자동화기기)" },
            ["atm"] = new[] { "ATM: ‘카드를 넣어 주세요’. You have no Korean card yet.", "ATM: ‘카드를 넣어 주세요’. Bạn chưa có thẻ Hàn.", "ATM: ‘카드를 넣어 주세요’. Kamu belum punya kartu Korea." },
        };
    }
}
