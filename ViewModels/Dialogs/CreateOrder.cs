using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;
using static PartyAIControls.PAICustomOrder;

namespace PartyAIControls.ViewModels.Dialogs
{
  internal class CreateOrder
  {
    private static Action _onCreateOrder;
    private static Hero _hero;
    private static PartyAIClanPartySettings _settings;
    private static bool _fallback;

    private static readonly string _titleText = new TextObject("{=PAIUq8Q1n8k}Choose which type of order to add").ToString();
    private static readonly string _landpatrolText = new TextObject("{=PAIaOu88dqT}Patrol an Area").ToString();
    private static readonly string _visitText = new TextObject("{=PAIIL6JG6Na}Visit A Settlement").ToString();
    private static readonly string _escortText = new TextObject("{=PAI1Et6heEa}Escort A Party").ToString();
    private static readonly string _recruitText = new TextObject("{=PAIyzzBSM4P}Recruit").ToString();
    private static readonly string _recruitHint = new TextObject("{=PAIHJFAtbk8}Order the party leader to only focus on recruiting troops. If they have an assigned troop template, they will only visit settlements that offer those troops. Keep in mind these settlements may be far away.").ToString();
    private static readonly string _stayInSettlementText = new TextObject("{=PAIOzsG1s1J}Stay In A Settlement").ToString();
    private static readonly string _landpatrolHintText = new TextObject("{=PAIPQxGUfhd}Patrol around the target settlement. The party will visit villages and towns to restock its troops and supplies. Bandits and other enemies will be chased down if the party leader believes they can be caught. The party will defend villages and castles/towns within its patrol radius from raids and sieges.").ToString();
    private static readonly string _visitHintText = new TextObject("{=PAIljAEpAKF}Visit a settlement but don't stay there.").ToString();
    private static readonly string _escortHintText = new TextObject("{=PAIEI3gTLMP}Escort a party").ToString();
    private static readonly string _stayInSettlementHintText = new TextObject("{=PAIVeQlQhCC}Stay in the settlement. Will not defend the settlement if it is under siege and the party is outside the walls.").ToString();
    private static readonly string _besiegeText = new TextObject("{=PAIgXDbzpdD}Besiege A Settlement").ToString();
    private static readonly string _besiegeHintText = new TextObject("{=PAIzxQXNul8}The party or army will besiege the target settlement. The order will be cleared upon capturing the city or by the attacking army being defeated.").ToString();
    private static readonly string _defendText = new TextObject("{=PAIgNGL6W5j}Defend A Settlement").ToString();
    private static readonly string _defendHintText = new TextObject("{=PAITZmUFJSB}Stay in the garrison of the settlement. The party may make occassional visits to other settlements for food if there is not enough food in the settlement to buy.").ToString();

    public static void Create(PartyAIClanPartySettings settings, Action callback, bool fallback = false)
    {
      _hero = settings.Hero;
      _settings = settings;
      _onCreateOrder = callback;
      _fallback = fallback;

      List<InquiryElement> list = new();

      list.Add(new InquiryElement(new PAICustomOrder(null, OrderType.PatrolAroundPoint), _landpatrolText, null, true, _landpatrolHintText));
      list.Add(new InquiryElement(new PAICustomOrder(null, OrderType.EscortParty), _escortText, null, true, _escortHintText));
      list.Add(new InquiryElement(new PAICustomOrder(null, OrderType.StayInSettlement), _stayInSettlementText, null, true, _stayInSettlementHintText));
      list.Add(new InquiryElement(new PAICustomOrder(null, OrderType.VisitSettlement), _visitText, null, true, _visitHintText));
      if (!_fallback)
      {
        list.Add(new InquiryElement(new PAICustomOrder(null, OrderType.BesiegeSettlement), _besiegeText, null, true, _besiegeHintText));
      }
      list.Add(new InquiryElement(new PAICustomOrder(null, OrderType.DefendSettlement), _defendText, null, true, _defendHintText));
      list.Add(new InquiryElement(new PAICustomOrder(null, OrderType.RecruitFromTemplate), _recruitText, null, true, _recruitHint));
      if (_fallback)
      {
        list.Add(new InquiryElement(new PAICustomOrder(null, OrderType.None), new TextObject("{=koX9okuG}None").ToString(), null, true, string.Empty));
      }

      MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(_titleText, string.Empty, list, isExitShown: true, 1, 1, GameTexts.FindText("str_next").ToString(), GameTexts.FindText("str_cancel").ToString(), CreateCallback, null, "", true));
    }

    private static void CreateCallback(List<InquiryElement> list)
    {
      if (list.Count == 0)
      {
        return;
      }

      PAICustomOrder order = (PAICustomOrder)list.First().Identifier;

      string title = null;
      List<InquiryElement> newList = new();
      title = new TextObject("{=PAIZScpdz8d}Select a target").ToString();

      switch (order.Behavior)
      {
        case OrderType.None:
        case OrderType.RecruitFromTemplate:
          ChooseTargetCallback(list);
          return;
        case OrderType.EscortParty:
          HashSet<MobileParty> parties = (_hero?.MapFaction?.WarPartyComponents.ConvertAll(p => p?.MobileParty)?.ToHashSet()) ?? new();
          if (_hero?.Clan?.Kingdom != null)
          {
            parties = parties.Concat(_hero.Clan.Kingdom.WarPartyComponents.ConvertAll(p => p?.MobileParty)).ToHashSet();
          }
          parties = parties.Concat(MobileParty.All.Where(m => m?.MapFaction != null && m.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D) <= MobileParty.MainParty.SeeingRange * 2f && !m.IsGarrison && !m.IsMilitia && !FactionManager.IsAtWarAgainstFaction(m.MapFaction, Hero.MainHero.MapFaction))).ToHashSet();

          foreach (MobileParty mobileParty in parties.OrderByDescending(s => s?.ActualClan?.Equals(_hero?.Clan)).ThenBy(s => s?.Name?.ToString()).ToList())
          {
            if (mobileParty == null) { continue; }
            PAICustomOrder insert = new(mobileParty, order.Behavior);
            CharacterObject character = ConversationHelper.GetConversationCharacterPartyLeader(mobileParty.Party);
            if (character == null)
            {
              newList.Add(new InquiryElement(insert, mobileParty.Name?.ToString(), null));
            }
            else
            {
              newList.Add(new InquiryElement(insert, mobileParty.Name?.ToString(), new CharacterImageIdentifier(CharacterCode.CreateFrom(character))));
            }
          }
          break;
        case OrderType.VisitSettlement:
        case OrderType.StayInSettlement:
        case OrderType.DefendSettlement:
          List<Settlement> settlements = Settlement.All.Where(s => s.IsFortification || s.IsVillage).Where(s => !FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, s.MapFaction)).ToList();

          foreach (Settlement settlement in settlements.OrderByDescending(s => s.OwnerClan.Equals(_hero?.Clan)).ThenByDescending(s => s.IsTown).ThenByDescending(s => s.IsCastle).ThenBy(s => s.Name.ToString()).ToList())
          {
            PAICustomOrder insert = new(settlement, order.Behavior);

            newList.Add(new InquiryElement(insert, settlement.Name.ToString(), new BannerImageIdentifier(settlement.OwnerClan?.Banner, false)));
          }
          break;
        case OrderType.PatrolAroundPoint:
          settlements = Settlement.All.Where(s => s.IsFortification || s.IsVillage).ToList();
          newList.Add(new(new PAICustomOrder(null, OrderType.PatrolClanLands), new TextObject("{=PAIb2F6Hyfs}Patrol Clan Territory").ToString(), new BannerImageIdentifier(_hero?.ClanBanner, false)));
          foreach (Settlement settlement in settlements.OrderByDescending(s => s.OwnerClan.Equals(_hero?.Clan)).ThenByDescending(s => s.IsTown).ThenByDescending(s => s.IsCastle).ThenBy(s => s.Name.ToString()).ToList())
          {
            PAICustomOrder insert = new(settlement, order.Behavior);

            newList.Add(new InquiryElement(insert, settlement.Name.ToString(), new BannerImageIdentifier(settlement.MapFaction?.Banner, false)));
          }
          break;
        case OrderType.BesiegeSettlement:
          foreach (Settlement settlement in Settlement.All.Where(s => FactionManager.IsAtWarAgainstFaction(s.MapFaction, Hero.MainHero.MapFaction) && s.IsFortification).OrderByDescending(s => s.IsTown).ThenBy(s => s.Name.ToString()).ToList())
          {
            PAICustomOrder insert = new(settlement, order.Behavior);

            newList.Add(new InquiryElement(insert, settlement.Name.ToString(), new BannerImageIdentifier(settlement.MapFaction?.Banner, false)));
          }
          break;
        default:
          break;
      }

      MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, null, newList, isExitShown: true, 1, 1, GameTexts.FindText("str_done").ToString(), GameTexts.FindText("str_cancel").ToString(), ChooseTargetCallback, null, null, true));
    }

    private static void ChooseTargetCallback(List<InquiryElement> list)
    {
      if (list.Count == 0)
      {
        return;
      }

      PAICustomOrder order = (PAICustomOrder)list.First().Identifier;
      if (_fallback)
      {
        _settings.FallbackOrder = order;
      }
      else
      {
        if (_settings.HasActiveOrder)
        {
          _settings.OrderQueue.Add(order);
        }
        else
        {
          _settings.SetOrder(order);
        }
      }

      _onCreateOrder.Invoke();
    }
  }
}
