using MySql.Data.MySqlClient;

namespace PineTree.Classes
{
    public static class OrderItemEditorStateRepository
    {
        internal static void Save(MySqlConnection connection, MySqlTransaction transaction, long itemId, string json)
        {
            // Older dialogs do not edit this state and must preserve it.
            if (json == null) return;
            BannerOrderEditorState.FromJson(json);
            using (var command = new MySqlCommand(@"
INSERT INTO OrderItemEditorStates (OrderItemId, EditorType, StateJSON)
VALUES (@Item, '현수막', @State)
ON DUPLICATE KEY UPDATE EditorType = VALUES(EditorType), StateJSON = VALUES(StateJSON)", connection, transaction))
            {
                command.Parameters.AddWithValue("@Item", itemId);
                command.Parameters.AddWithValue("@State", json);
                command.ExecuteNonQuery();
            }
        }
    }
}
