using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System;

public class BtnEventTrigger : EventTrigger
{
    private EventTrigger trigger;
    
    public override void OnPointerClick(PointerEventData eventData)
    {
        OnBtnClick();
        base.OnPointerClick(eventData);
    }
    public override void OnSubmit(BaseEventData eventData)
    {
        OnBtnClick();
        base.OnSubmit(eventData);
    }
    public override void OnPointerEnter(PointerEventData eventData)
    {
        OnBtnSelect();
        base.OnPointerEnter(eventData);
    }
    public override void OnSelect(BaseEventData eventData)
    {
        OnBtnSelect();
        base.OnSelect(eventData);
    }
    public override void OnPointerExit(PointerEventData eventData)
    {
        OnBtnDeselect();
        base.OnPointerExit(eventData);
    }
    public override void OnDeselect(BaseEventData eventData)
    {
        OnBtnDeselect();
        base.OnDeselect(eventData);
    }

    protected virtual void OnBtnClick() { }
    protected virtual void OnBtnSelect() { }
    protected virtual void OnBtnDeselect() { }

}
