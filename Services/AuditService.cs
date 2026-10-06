using System;
using System.Data.SQLite;
using ApotekFirdanFarma.Data;
using ApotekFirdanFarma.Models;

namespace ApotekFirdanFarma.Services
{
    public static class AuditService
    {
        public static void LogDetailed(
            AppUser user,
            string action,
            string entity,
            long? entityId = null,
            string referenceCode = null,
            string oldValue = null,
            string newValue = null,
            double? amount = null,
            double? discount = null,
            string paymentMethod = null,
            string reason = null,
            string notes = null,
            string status = "BERHASIL",
            long? medicineId = null,
            string batchNo = null,
            int qtyIn = 0,
            int qtyOut = 0,
            SQLiteConnection c = null,
            SQLiteTransaction tx = null,
            string usernameOverride = null,
            string roleOverride = null)
        {
            var owns = c == null;
            try
            {
                if (owns) c = Database.Open();
                using (var cmd = new SQLiteCommand(@"INSERT INTO audit_logs(
 action,entity,entity_id,user_id,username,role,reason,amount,discount,payment_method,transaction_code,
 medicine_id,batch_no,qty_in,qty_out,old_value,new_value,status,notes,created_at)
VALUES(@a,@e,@eid,@uid,@un,@role,@reason,@amount,@discount,@pay,@code,@mid,@batch,@qin,@qout,@old,@new,@status,@notes,@now)", c, tx))
                {
                    cmd.Parameters.AddWithValue("@a", action ?? "UNKNOWN");
                    cmd.Parameters.AddWithValue("@e", (object)entity ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@eid", (object)entityId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@uid", user == null ? (object)DBNull.Value : user.Id);
                    cmd.Parameters.AddWithValue("@un", user == null ? (string.IsNullOrWhiteSpace(usernameOverride) ? (object)DBNull.Value : usernameOverride) : (object)user.Username);
                    cmd.Parameters.AddWithValue("@role", user == null ? (string.IsNullOrWhiteSpace(roleOverride) ? (object)DBNull.Value : roleOverride) : (object)user.Role);
                    cmd.Parameters.AddWithValue("@reason", (object)reason ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@amount", (object)amount ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@discount", (object)discount ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pay", (object)paymentMethod ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@code", (object)referenceCode ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@mid", (object)medicineId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@batch", (object)batchNo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@qin", qtyIn);
                    cmd.Parameters.AddWithValue("@qout", qtyOut);
                    cmd.Parameters.AddWithValue("@old", (object)oldValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@new", (object)newValue ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", status ?? "BERHASIL");
                    cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                if (owns && c != null) c.Dispose();
            }
        }

        // Kompatibilitas untuk pemanggilan kode v4.
        public static void Log(AppUser user, string action, string entity, long? entityId, string reason, double? amount, string payment, string code, string notes, SQLiteConnection c = null, SQLiteTransaction tx = null)
        {
            LogDetailed(user, action, entity, entityId, code, null, null, amount, null, payment, reason, notes, "BERHASIL", null, null, 0, 0, c, tx);
        }
    }
}
