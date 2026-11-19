using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public static class TextInputFocus
{
    public static bool IsTyping()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
            return false;

        GameObject selected = eventSystem.currentSelectedGameObject;

        if (selected == null)
            return false;

        TMP_InputField field = selected.GetComponent<TMP_InputField>();

        if (field == null)
            return false;

        return field.isFocused;
    }
}
