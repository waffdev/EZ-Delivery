using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using UnityEngine;
using MyBox;

namespace EZDelivery
{
    
    public class EZDelivery : MelonMod
    {

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                GameObject player = GameObject.Find("Player");
                if (player != null)
                {
                    BoxInteraction bi = player.GetComponent<BoxInteraction>();
                    if (bi != null)
                    {
                        if (bi.Interactable is Box)
                        {
                            PlaceBoxInRack(player, bi); 
                        }
                    }
                }
            }
        }

        private void PlaceBoxInRack(GameObject player, BoxInteraction boxInteraction)
        {
            Box box = (Box)boxInteraction.Interactable;
            RackManager rackManager = Singleton<RackManager>.Instance;
            if (rackManager == null)
                return;

            ProductSO product = box.Product;

            RackSlot rackSlot = rackManager.GetRackSlotThatHasSpaceFor(product.ID, box.BoxID);
            if (rackSlot.Data.ProductID == product.ID)
            {
                box.CloseBox();
                rackSlot.AddBox(box.BoxID, box);
                box.Racked = true;

                Singleton<PlayerObjectHolder>.Instance.PlaceBoxToRack();
                Singleton<PlayerInteraction>.Instance.InteractionEnd(boxInteraction);
            } else
            {
                Singleton<WarningSystem>.Instance.RaiseInteractionWarning(InteractionWarningType.FULL_RACK, null);
            }
            
            
        }
    }

    
}



  
