using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nj4x.wymiatacz_fx.Includes;
using nj4x.wymiatacz_fx.Environment;
using nj4x.Metatrader;

namespace nj4x.wymiatacz_fx.Position
{
  /// <summary>
  /// Manager pozycji.
  /// </summary>
  public class PositionManager
  {
    /// <summary>
    /// Referencja do obiektu strategii (przydaje się w initialize()).
    /// </summary>
    private static nj4x.wymiatacz_fx.Strategy.Marcinek mt4 = null;
    /// <summary>
    /// Referencja do obiektu loggera.
    /// </summary>
    private static log4net.ILog Logger;

    /// <summary>
    /// Ważny poziom SR w (dla otwarcia pozycji strict) nad obecną ceną.
    /// </summary>
    public decimal imp_SR_for_strict_upper { get; private set; }
    /// <summary>
    /// Ważny poziom SR w (dla otwarcia pozycji strict) pod obecną ceną.
    /// </summary>
    public decimal imp_SR_for_strict_lower { get; private set; }

    /// <summary>
    /// Wszystkie pozycje (kiedykolwiek otwarte): (id, pozycja).
    /// </summary>
    public Dictionary<int, Position> positions { get; private set; }
    /// <summary>
    /// Pozycje o nieznanym statusie otwarcia (dostaliśmy timeout): (id_counter, pozycja).
    /// </summary>
    public Dictionary<int, Position> unknown_positions { get; private set; }
    /// <summary>
    /// Tickety pozycji częściowo zamkniętych (jako wartości - oczekiwane rozmiary nowych pozycji) gdy jeszcze nie zaksięgowaliśmy pozycji kontynuującej.
    /// </summary>
    public Dictionary<int, double> unbound_predecessors { get; private set; }
    /// <summary>
    /// Aktywne pozycje ze strategią agresywną (ticket, pozycja).
    /// </summary>
    public Dictionary<int, Position> pos_aggressive { get; private set; }
    /// <summary>
    /// Aktywne pozycje ze strategią zachowawczą (ticket, pozycja).
    /// </summary>
    public Dictionary<int, Position> pos_strict { get; private set; }
    /// <summary>
    /// Aktywne pozycje ze strategią pikową (ticket, pozycja).
    /// </summary>
    public Dictionary<int, Position> pos_peak { get; private set; }
    /// <summary>
    /// Czy czasowa blokada otwierania nowych pozycji aktywna.
    /// </summary>
    public bool dont_open { get; private set; }
    /// <summary>
    /// Termin (czas z komputera lokalnego) upłynięcia blokady dont_open.
    /// </summary>
    public DateTime dont_open_till { get; private set; }
    /// <summary>
    /// Pozycje (id, true) aktywne (otwarte lub oczekujące) w MT4.
    /// </summary>
    public Dictionary<int, bool> active_positions_mt4 { get; private set; }
    /// <summary>
    /// Pozycje (id, true) aktywne (otwarte lub oczekujące) w aplikacji.
    /// </summary>
    public Dictionary<int, bool> active_positions_robot { get; private set; }
    /// <summary>
    /// Pozycje (id, true) aktywne (otwarte lub oczekujące) w MT4 ale nie w robocie.
    /// </summary>
    public Dictionary<int, bool> unexpected_active_positions_mt4 { get; private set; }
    /// <summary>
    /// Pozycje (id, true) nieaktywne w MT4 ale aktywne w robocie.
    /// </summary>
    public Dictionary<int, bool> disactivated_positions_mt4 { get; private set; }
    /// <summary>
    /// Historyczne (nieaktywne) pozycje odkryte przy księgowaniu początkowym, nie mające odpowiedników w positions.
    /// </summary>
    public HashSet<int> nonadopted_oldies { get; private set; }

    /// <summary>
    /// Znalezione podczas ewidencjonowania pozycji nowe pozycje bedace wynikiem częściowego zamknięcia starych.
    /// </summary>
    public List<Tuple<Position, double, Enums.PositionStatus, double, DateTime, String, double, Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>>>> found_successors { get; private set; }

    /// <summary>
    /// Pomocniczy licznik umieszczany w komentarzu otwieranej pozycji, żeby łatwiej identyfikowac otwarte z opóźnieniem (gdy nie znamy ticketu od razu).
    /// </summary>
    private int id_counter = 1;

    /// <summary>
    /// Ile minut tworzy słupek na podstawowym wykresie czasowym.
    /// </summary>
    public readonly int minutes_per_bar;

    /// <summary>
    /// Czy istnieje przynajmniej jedna aktywna pozycja Peak.
    /// </summary>
    public bool hasPeak { get; private set; }
    /// <summary>
    /// Czy istnieje przynajmniej jedna aktywna pozycja Aggressive.
    /// </summary>
    public bool hasAggressive { get; private set; }
    /// <summary>
    /// Czy istnieje przynajmniej jedna aktywna pozycja Strict.
    /// </summary>
    public bool hasStrict { get; private set; }


    /// <summary>
    /// Minimalny dopuszczalny przez robota wolny depozyt w walucie depozytowej rachunku.
    /// </summary>
    public decimal stopout { get; private set; }
    /// <summary>
    /// Wolny depozyt do otwierania pozycji.
    /// </summary>
    public decimal free_margin { get; private set; }
    /// <summary>
    /// Wolny depozyt do otwierania pozycji możliwy do użycia (nie naruszający poziomu stopout plus margines bezpieczeństwa).
    /// </summary>
    public decimal usable_free_margin { get; private set; }
    /// <summary>
    /// Depozyt (tak naprawdę equity, ale u nas bardziej surowo: free margin) musi przekraczać tę wartość aby nie było margin call dla otwartych już pozycji.
    /// </summary>
    public decimal used_free_margin_mc_current { get; private set; }
    /// <summary>
    /// Depozyt (tak naprawdę equity, ale u nas bardziej surowo: free margin) musi przekraczać tę wartość aby nie było margin call dla otwieranej pozycji o rozmiarze 1 lota.
    /// </summary>
    public decimal used_free_margin_mc_new { get; private set; }
    /// <summary>
    /// Wolny depozyt do otwierania pozycji możliwy do użycia (nie naruszający poziomu margin call plus margines bezpieczeństwa).
    /// </summary>
    public decimal remaining_free_margin { get; private set; }
    /// <summary>
    /// Jak dużą pozycję (w lotach) możemy jeszcze otworzyć bez narażania się na margin call ani stopout.
    /// </summary>
    public decimal allowed_new_lots { get; private set; }

    /// <summary>
    /// Suma wolumenów (w lotach) otwartych i oczekujących (a także ze statusem nieustalonym) pozycji.
    /// </summary>
    public decimal total_positions_volume { get; private set; }
    /// <summary>
    /// Czy było już wydane ostrzeżenie o zbliżaniu się do poziomu margin call (żeby nie logować za dużo za kolejnym razem).
    /// </summary>
    private bool margin_call_warning_issued = false;
    /// <summary>
    /// Czy było już wydane ostrzeżenie o zbliżaniu się do poziomu stopout (żeby nie logować za dużo za kolejnym razem).
    /// </summary>
    private bool stopout_warning_issued = false;

    /// <summary>
    /// Ticket ostatnio otwartej pozycji peak (lub -1 gdy żadnej nie było).
    /// </summary>
    public int lastOpenedPeak { get; private set; }

    /// <summary>
    /// Stwórz obiekt managera pozycji.
    /// </summary>
    /// <param name="_mt4">Referencja do obiektu strategii.</param>
    /// <param name="_Logger">Referencja do obiektu loggera.</param>
    /// <param name="_minutes_per_bar">Ile minut na słupek jest w podstawowym wykresie.</param>
    public PositionManager(nj4x.wymiatacz_fx.Strategy.Marcinek _mt4, log4net.ILog _Logger, int _minutes_per_bar)
    {
      positions = new Dictionary<int, Position>(500);
      unknown_positions = new Dictionary<int, Position>(15);
      pos_aggressive = new Dictionary<int, Position>(5);
      pos_strict = new Dictionary<int, Position>(5);
      pos_peak = new Dictionary<int, Position>(5);
      active_positions_mt4 = new Dictionary<int, bool>(15);
      active_positions_robot = new Dictionary<int, bool>(15);
      unexpected_active_positions_mt4 = new Dictionary<int, bool>(15);
      disactivated_positions_mt4 = new Dictionary<int, bool>(15);
      unbound_predecessors = new Dictionary<int, double>(15);
      nonadopted_oldies = new HashSet<int>();
      found_successors = new List<Tuple<Position, double, Enums.PositionStatus, double, DateTime, String, double, Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>>>>(15);
      dont_open = false;
      mt4 = _mt4;
      Logger = _Logger;
      minutes_per_bar = _minutes_per_bar;
      hasAggressive = hasPeak = hasStrict = false;
      lastOpenedPeak = -1;
    }

    /// <summary>
    /// Uaktualnij imp_SR_for_strict_upper i imp_SR_for_strict_lower.
    /// </summary>
    /// <param name="_imp_SR_for_strict_upper">Nowa wartość imp_SR_for_strict_upper.</param>
    /// <param name="_imp_SR_for_strict_lower">Nowa wartość imp_SR_for_strict_lower.</param>
    public void updateImpSRForStrict(decimal _imp_SR_for_strict_upper, decimal _imp_SR_for_strict_lower)
    {
      imp_SR_for_strict_upper = _imp_SR_for_strict_upper;
      imp_SR_for_strict_lower = _imp_SR_for_strict_lower;
    }

    /// <summary>
    /// Czy możemy otwierać pozycje (nie ma blokady po timeoucie).
    /// </summary>
    /// <returns>Czy możemy otwierać pozycje.</returns>
    public bool canOpenNew()
    {
      if (dont_open)
      {
        if (DateTime.UtcNow <= dont_open_till) return false;
        else dont_open = false;
      }

      return true;
    }

    /// <summary>
    /// Ustaw czasową blokadę otwierania nowych pozycji (bo nie wiemy czy ostatnia została otwarta).
    /// </summary>
    private void setDontOpen()
    {
      Logger.Warn("Setting setDontOpen mode on " + DateTime.UtcNow + ":\n" + System.Environment.StackTrace);
      dont_open = true;
      dont_open_till = DateTime.UtcNow + LocalConfig.ORDER_CHECK_DELAY_TIMESPAN;
    }

    /// <summary>
    /// Zrób księgowanie pozycji.
    /// </summary>
    /// <param name="thorough">Czy przetworzyć wszystkie historyczne.</param>
    private void accounting(bool thorough = false)
    {
      activeAccounting();
      nonActiveAccounting(!thorough);
      successorsAccounting();
    }

    /// <summary>
    /// Zrób księgowanie pozycji aktywnych. Uaktualnia: active_positions_mt4, active_positions_robot, unexpected_active_positions_mt4, disactivated_positions_mt4, found_successors i znalezione pozycje oprócz nowych powstałych przez częściowe zamknięcie starych. Może odpalić setEmergencyMode(). Zwraca czy wszystko ok.
    /// </summary>
    /// <returns>Zwraca czy wszystko ok.</returns>
    private bool activeAccounting()
    {
      int repeats = 3;
      bool dont_open_pos = false;
      active_positions_mt4.Clear();
      active_positions_robot.Clear();
      unexpected_active_positions_mt4.Clear();
      disactivated_positions_mt4.Clear();
      found_successors.Clear();
      foreach (var x in pos_aggressive.Keys) active_positions_robot[pos_aggressive[x].id] = true;
      foreach (var x in pos_strict.Keys) active_positions_robot[pos_strict[x].id] = true;
      foreach (var x in pos_peak.Keys) active_positions_robot[pos_peak[x].id] = true;
      while (repeats-- > 0)
      {
        dont_open_pos = false;
        try
        {
          for (int i = mt4.OrdersTotal() - 1; i >= 0; i--)
          {
            if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_TRADES))
            {
              int OrderTicket = mt4.OrderTicket();
              int OrderMagicNumber = mt4.OrderMagicNumber();
              if (Lib.isIntToMagicOk(OrderMagicNumber))
              {
                active_positions_mt4[OrderTicket] = true;
                double OrderClosePrice = mt4.OrderClosePrice();
                DateTime OrderCloseTime = mt4.OrderCloseTime();
                string OrderComment = mt4.OrderComment();
                double OrderCommission = mt4.OrderCommission();
                DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
                OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
                double OrderLots = mt4.OrderLots();
                double OrderOpenPrice = mt4.OrderOpenPrice();
                DateTime OrderOpenTime = mt4.OrderOpenTime();
                double OrderProfit = mt4.OrderProfit();
                double OrderStopLoss = mt4.OrderStopLoss();
                double OrderSwap = mt4.OrderSwap();
                string OrderSymbol = mt4.OrderSymbol();
                double OrderTakeProfit = mt4.OrderTakeProfit();
                TradeOperation OrderType = mt4.OrderType();
                Enums.PositionStatus status = (OrderType == TradeOperation.OP_BUY || OrderType == TradeOperation.OP_SELL ? Enums.PositionStatus.Opened : Enums.PositionStatus.Pending);
                if (active_positions_robot.ContainsKey(OrderTicket))
                {
                  bool pom = positions[OrderTicket].update(status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
                  if (!pom)
                  {
                    dont_open_pos = true;
                    string str = "activeAccounting(): Error in updating " + OrderTicket;
                    Logger.Error(str);
                  }
                }
                else
                {
                  string id_str = Position.idFromComment(OrderComment);
                  int id = -1;
                  if (id_str != "") id = Convert.ToInt32(id_str);
                  if (unknown_positions.ContainsKey(id))
                  {
                    // przy timeoucie może coś być
                    TimeSpan ts = DateTime.UtcNow - unknown_positions[id].creation_time_local;
                    if (ts > LocalConfig.ORDER_CHECK_DELAY_TIMESPAN)
                    {
                      Logger.Warn("activeAccounting(): Belatedly (after " + ts + ") found unknown position " + OrderTicket + " (" + id_str + ")");
                    }
                    bool pom = unknown_positions[id].update(status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
                    if (!pom)
                    {
                      dont_open_pos = true;
                      string str = "activeAccounting(): Error in updating unknown " + OrderTicket + " (" + id_str + ")";
                      Logger.Error(str);
                    }
                    else
                    {
                      Logger.Info("activeAccounting(): Found and updated " + OrderTicket + " (" + id_str + ")");
                      positions[OrderTicket] = unknown_positions[id];
                      unknown_positions.Remove(id);
                      if (positions[OrderTicket].tactics == Enums.PositionTactics.Strict)
                      {
                        pos_strict[OrderTicket] = positions[OrderTicket];
                      }
                      else if (positions[OrderTicket].tactics == Enums.PositionTactics.Peak)
                      {
                        pos_peak[OrderTicket] = positions[OrderTicket];
                      }
                      else
                      {
                        pos_aggressive[OrderTicket] = positions[OrderTicket];
                      }
                    }
                  }
                  else
                  {
                    id = Position.predFromComment(OrderComment);
                    // id jest identyfikatorem pozycji zamykanej a nie zamykającej, więc jeśli zamykająca była większa (i jest zapisana w unbound_predecessors) - musimy odnaleźć jej id
                    bool f = unbound_predecessors.ContainsKey(id);
                    if (!f && id != -1)
                    {
                      id = positions[id].closed_by;
                      f = unbound_predecessors.ContainsKey(id);
                    }
                    if (f)
                    {
                      // zapamiętać parametry i po całym księgowaniu (żeby predecessor był uaktualniony) - wywołać Position.createSuccessor()
                      Logger.Info("activeAccounting(): Found " + OrderTicket + " (successor of " + id + ")");
                      Tuple<double, String, double, int, TradeOperation> obejscie = Tuple.Create(OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
                      Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>> microsoftowej = new Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>>(OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, new Tuple<double, String, double, int, TradeOperation>(OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType));
                      Tuple<Position, double, Enums.PositionStatus, double, DateTime, String, double, Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>>> chujozy = new Tuple<Position, double, Enums.PositionStatus, double, DateTime, String, double, Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>>>(positions[id], unbound_predecessors[id], status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, microsoftowej);
                      found_successors.Add(chujozy);
                    }
                    else
                    {
                      // raczej poważny błąd
                      // być może był timeout przy częściowym zamykaniu pozycji (sprawdzić po zrobieniu pełnego księgowania pozycji?)
                      unexpected_active_positions_mt4[OrderTicket] = true;
                      dont_open_pos = true;
                      Logger.Error("activeAccounting(): !unbound_predecessors(" + id + "), ticket: " + OrderTicket);
                    }
                  }
                }
              }
            }
            else
            {
              dont_open_pos = true;
              string str = "activeAccounting(): Error in OrderSelect(" + i + ", SELECT_BY_POS, MODE_TRADES)";
              int err_no = mt4.GetLastError();
              str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
              Logger.Error(str);
            }
          }
        }
        catch (Exception e)
        {
          string str = "activeAccounting(): exception caught";
          int err_no = mt4.GetLastError();
          str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
          str += e;
          Logger.Error(str);
          throw;
        }
        if (!dont_open_pos) break;
      }

      if (dont_open_pos) mt4.setEmergencyMode();

      foreach (var x in active_positions_robot)
      {
        if (!active_positions_mt4.ContainsKey(x.Key)) disactivated_positions_mt4[x.Key] = true;
      }

      return !dont_open_pos;
    }

    /// <summary>
    /// Zrób księgowanie pozycji nieaktywnych. Korzysta i modyfikuje: disactivated_positions_mt4, found_successors i znalezione pozycje oprócz nowych powstałych przez częściowe zamknięcie starych. Może odpalić setEmergencyMode(). Zwraca czy wszystko ok.
    /// </summary>
    /// <returns>Zwraca czy wszystko ok.</returns>
    private bool nonActiveAccounting(bool limited = false)
    {
      bool ok = true;
      int repeats = 3;
      while (repeats-- > 0)
      {
        ok = true;
        for (int i = mt4.OrdersHistoryTotal() - 1; i >= 0; i--)
        {
          if (limited)
          {
            if (disactivated_positions_mt4.Count() == 0 && unbound_predecessors.Count() == 0) break;
          }
          try
          {
            if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_HISTORY))
            {
              int OrderTicket = mt4.OrderTicket();
              int OrderMagicNumber = mt4.OrderMagicNumber();
              if (Lib.isIntToMagicOk(OrderMagicNumber))
              {
                string OrderComment = mt4.OrderComment();
                int id_pred = Position.predFromComment(OrderComment);
                // id jest identyfikatorem pozycji zamykanej a nie zamykającej, więc jeśli zamykająca była większa (i jest zapisana w unbound_predecessors) - musimy odnaleźć jej id
                if (!unbound_predecessors.ContainsKey(id_pred) && id_pred != -1 && unbound_predecessors.ContainsKey(positions[id_pred].closed_by)) id_pred = positions[id_pred].closed_by;
                if (limited)
                {
                  if (!disactivated_positions_mt4.ContainsKey(OrderTicket) && !unbound_predecessors.ContainsKey(id_pred)) continue;
                }
                double OrderClosePrice = mt4.OrderClosePrice();
                DateTime OrderCloseTime = mt4.OrderCloseTime();
                double OrderCommission = mt4.OrderCommission();
                DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
                OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
                double OrderLots = mt4.OrderLots();
                double OrderOpenPrice = mt4.OrderOpenPrice();
                DateTime OrderOpenTime = mt4.OrderOpenTime();
                double OrderProfit = mt4.OrderProfit();
                double OrderStopLoss = mt4.OrderStopLoss();
                double OrderSwap = mt4.OrderSwap();
                string OrderSymbol = mt4.OrderSymbol();
                double OrderTakeProfit = mt4.OrderTakeProfit();
                TradeOperation OrderType = mt4.OrderType();
                Enums.PositionStatus status = (OrderType == TradeOperation.OP_BUY || OrderType == TradeOperation.OP_SELL ? Enums.PositionStatus.Closed : Enums.PositionStatus.Deleted);
                if (positions.ContainsKey(OrderTicket))
                {
                  bool pom = positions[OrderTicket].update(status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
                  if (pom)
                  {
                    disactivated_positions_mt4.Remove(OrderTicket);
                    pos_aggressive.Remove(OrderTicket);
                    pos_strict.Remove(OrderTicket);
                    pos_peak.Remove(OrderTicket);
                  }
                  else
                  {
                    ok = false;
                    string str = "nonActiveAccounting(): Error in updating " + OrderTicket;
                    Logger.Error(str);
                  }
                }
                else
                {
                  string id_str = Position.idFromComment(OrderComment);
                  int id = -1;
                  if (id_str != "") id = Convert.ToInt32(id_str);
                  if (unknown_positions.ContainsKey(id))
                  {
                    // przy timeoucie może coś być
                    TimeSpan ts = DateTime.UtcNow - unknown_positions[id].creation_time_local;
                    if (ts > LocalConfig.ORDER_CHECK_DELAY_TIMESPAN)
                    {
                      Logger.Warn("nonActiveAccounting(): Belatedly (after " + ts + ") found unknown position " + OrderTicket + " (" + id_str + ")");
                    }
                    bool pom = unknown_positions[id].update(status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
                    if (!pom)
                    {
                      ok = false;
                      string str = "nonActiveAccounting(): Error in updating unknown " + OrderTicket + " (" + id_str + ")";
                      Logger.Error(str);
                    }
                    else
                    {
                      Logger.Info("nonActiveAccounting(): Found and updated " + OrderTicket + " (" + id_str + ")");
                      positions[OrderTicket] = unknown_positions[id];
                      unknown_positions.Remove(id);
                    }
                  }
                  else
                  {
                    if (unbound_predecessors.ContainsKey(id_pred))
                    {
                      // zapamiętać parametry i po całym księgowaniu (żeby predecessor był uaktualniony) - wywołać Position.createSuccessor()
                      Logger.Info("nonActiveAccounting(): Found closed " + OrderTicket + " (successor of " + id_pred + ")");
                      Tuple<double, String, double, int, TradeOperation> obejscie = Tuple.Create(OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
                      Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>> microsoftowej = new Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>>(OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, new Tuple<double, String, double, int, TradeOperation>(OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType));
                      Tuple<Position, double, Enums.PositionStatus, double, DateTime, String, double, Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>>> chujozy = new Tuple<Position, double, Enums.PositionStatus, double, DateTime, String, double, Tuple<DateTime, double, int, double, DateTime, double, double, Tuple<double, String, double, int, TradeOperation>>>(positions[id_pred], unbound_predecessors[id_pred], status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, microsoftowej);
                      found_successors.Add(chujozy);
                    }
                    else
                    {
                      if (!nonadopted_oldies.Contains(OrderTicket))
                      {
                        ok = false;
                        string str = "nonActiveAccounting(): unexpected historical position found " + OrderTicket + " " + OrderMagicNumber + " " + OrderClosePrice + " " + OrderCloseTime + " " + OrderCommission + " " + OrderExpiration + " " + OrderLots + " " + OrderOpenPrice + " " + OrderOpenTime + " " + OrderProfit + " " + OrderStopLoss + " " + OrderSwap + " " + OrderSymbol + " " + OrderTakeProfit + " " + OrderType + " " + OrderComment;
                        Logger.Error(str);
                      }
                    }
                  }
                }
              }
            }
            else
            {
              ok = false;
              string str = "nonActiveAccounting(): Error in OrderSelect(" + i + ")";
              int err_no = mt4.GetLastError();
              str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
              Logger.Error(str);
            }
          }
          catch (Exception e)
          {
            string str = "nonActiveAccounting(): exception caught";
            int err_no = mt4.GetLastError();
            str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
            str += e;
            Logger.Error(str);
            throw;
          }
        }
        if (ok) break;
      }

      if (disactivated_positions_mt4.Count() != 0)
      {
        ok = false;
        string str = "nonActiveAccounting(): detected totally lost position(s):\n";
        foreach (var x in disactivated_positions_mt4)
        {
          Position p = positions[x.Key];
          str += "\t" + p.id + " " + p.magic_num + " " + p.closing_price + " " + p.closing_time + " " + p.commission + " " + p.opening_expiration + " " + p.volume + " " + p.opening_price + " " + p.opening_time + " " + p.profit + " " + p.SL + " " + p.swap + " " + p.symbol + " " + p.TP + " " + p.operation + " " + p.status + " " + p.opening_comment + "\n";
        }
        Logger.Error(str);
      }

      if (!ok) mt4.setEmergencyMode();
      return ok;
    }

    /// <summary>
    /// Zrób księgowanie pozycji nowych powstałych przez częściowe zamknięcie starych. Korzysta i modyfikuje: found_successors. Zwraca czy wszystko ok.
    /// </summary>
    /// <returns>Zwraca czy wszystko ok.</returns>
    private void successorsAccounting()
    {
      foreach (var x in found_successors)
      {
        Position p = Position.createSuccessor(x.Item1, x.Item2, x.Item3, x.Item4, x.Item5, x.Item6, x.Item7, x.Rest.Item1, x.Rest.Item2, x.Rest.Item3, x.Rest.Item4, x.Rest.Item5, x.Rest.Item6, x.Rest.Item7, x.Rest.Rest.Item1, x.Rest.Rest.Item2, x.Rest.Rest.Item3, x.Rest.Rest.Item4, x.Rest.Rest.Item5);
        int OrderTicket = x.Rest.Rest.Item4;
        positions[OrderTicket] = p;
        if (p.status == Enums.PositionStatus.Pending || p.status == Enums.PositionStatus.Opened)
        {
          if (p.tactics == Enums.PositionTactics.Strict)
          {
            pos_strict[OrderTicket] = p;
          }
          else if (p.tactics == Enums.PositionTactics.Peak)
          {
            pos_peak[OrderTicket] = p;
          }
          else
          {
            pos_aggressive[OrderTicket] = p;
          }
        }
        unbound_predecessors.Remove(x.Item1.id);
      }
      found_successors.Clear();
    }

    /// <summary>
    /// Wyszukuje i auktualnia (w positions lub unknown_positions) pozycję. Zwraca czy znaleziono.
    /// </summary>
    /// <param name="id">Ticket dla znanej pozycji, id z komentarza dla pozycji o nieznanym statusie.</param>
    /// <param name="unknown">Czy pozycja o nieznanym statusie.</param>
    /// <returns>Czy znaleziono i uaktualniono.</returns>
    public bool checkOrder(int id, bool unknown = false)
    {
      bool found = false;
      string id_str = id.ToString();
      Position posref;
      if (unknown)
      {
        posref = unknown_positions[id];
        int repeats = 3;
        bool problem = false;
        while (repeats-- > 0)
        {
          problem = false;
          try
          {
            for (int i = mt4.OrdersTotal() - 1; i >= 0; i--)
            {
              if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_TRADES))
              {
                int OrderTicket = mt4.OrderTicket();
                int OrderMagicNumber = mt4.OrderMagicNumber();
                string OrderComment = mt4.OrderComment();
                if (Lib.isIntToMagicOk(OrderMagicNumber) && id_str == Position.idFromComment(OrderComment))
                {
                  found = true;
                  double OrderClosePrice = mt4.OrderClosePrice();
                  DateTime OrderCloseTime = mt4.OrderCloseTime();
                  double OrderCommission = mt4.OrderCommission();
                  DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
                  OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
                  double OrderLots = mt4.OrderLots();
                  double OrderOpenPrice = mt4.OrderOpenPrice();
                  DateTime OrderOpenTime = mt4.OrderOpenTime();
                  double OrderProfit = mt4.OrderProfit();
                  double OrderStopLoss = mt4.OrderStopLoss();
                  double OrderSwap = mt4.OrderSwap();
                  string OrderSymbol = mt4.OrderSymbol();
                  double OrderTakeProfit = mt4.OrderTakeProfit();
                  TradeOperation OrderType = mt4.OrderType();
                  Enums.PositionStatus status = (OrderType == TradeOperation.OP_BUY || OrderType == TradeOperation.OP_SELL ? Enums.PositionStatus.Opened : Enums.PositionStatus.Pending);
                  bool pom = posref.update(status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
                  if (!pom)
                  {
                    problem = true;
                    string str = "checkOrder(): Error in updating (unknown) " + OrderTicket + " (" + id_str + ")";
                    Logger.Error(str);
                  }
                  else
                  {
                    Logger.Info("checkOrder(): Found and updated " + OrderTicket + " (" + id_str + ")");
                    positions[OrderTicket] = posref;
                    unknown_positions.Remove(id);
                    if (positions[OrderTicket].tactics == Enums.PositionTactics.Strict)
                    {
                      pos_strict[OrderTicket] = positions[OrderTicket];
                    }
                    else if (positions[OrderTicket].tactics == Enums.PositionTactics.Peak)
                    {
                      pos_peak[OrderTicket] = positions[OrderTicket];
                    }
                    else
                    {
                      pos_aggressive[OrderTicket] = positions[OrderTicket];
                    }
                  }
                  break;
                }
              }
              else
              {
                problem = true;
                string str = "checkOrder(): Error in OrderSelect(" + i + ", SELECT_BY_POS, MODE_TRADES)";
                int err_no = mt4.GetLastError();
                str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
                Logger.Error(str);
              }
            }
          }
          catch (Exception e)
          {
            string str = "checkOrder(): exception caught";
            int err_no = mt4.GetLastError();
            str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
            str += e;
            Logger.Error(str);
            throw;
          }
          if (!problem) break;
        }
        if (problem) mt4.setEmergencyMode();
        else if (!found)
        {
          repeats = 3;
          problem = false;
          while (repeats-- > 0)
          {
            problem = false;
            try
            {
              for (int i = mt4.OrdersHistoryTotal() - 1; i >= 0; i--)
              {
                if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_HISTORY))
                {
                  int OrderTicket = mt4.OrderTicket();
                  int OrderMagicNumber = mt4.OrderMagicNumber();
                  string OrderComment = mt4.OrderComment();
                  if (Lib.isIntToMagicOk(OrderMagicNumber) && id_str == Position.idFromComment(OrderComment))
                  {
                    found = true;
                    double OrderClosePrice = mt4.OrderClosePrice();
                    DateTime OrderCloseTime = mt4.OrderCloseTime();
                    double OrderCommission = mt4.OrderCommission();
                    DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
                    OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
                    double OrderLots = mt4.OrderLots();
                    double OrderOpenPrice = mt4.OrderOpenPrice();
                    DateTime OrderOpenTime = mt4.OrderOpenTime();
                    double OrderProfit = mt4.OrderProfit();
                    double OrderStopLoss = mt4.OrderStopLoss();
                    double OrderSwap = mt4.OrderSwap();
                    string OrderSymbol = mt4.OrderSymbol();
                    double OrderTakeProfit = mt4.OrderTakeProfit();
                    TradeOperation OrderType = mt4.OrderType();
                    Enums.PositionStatus status = (OrderType == TradeOperation.OP_BUY || OrderType == TradeOperation.OP_SELL ? Enums.PositionStatus.Closed : Enums.PositionStatus.Deleted);
                    bool pom = posref.update(status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
                    if (!pom)
                    {
                      problem = true;
                      string str = "checkOrder(): Error in updating historical (unknown) " + OrderTicket + " (" + id_str + ")";
                      Logger.Error(str);
                    }
                    else
                    {
                      Logger.Info("checkOrder(): Found and updated historical " + OrderTicket + " (" + id_str + ")");
                      positions[OrderTicket] = posref;
                      unknown_positions.Remove(id);
                    }
                    break;
                  }
                }
                else
                {
                  problem = true;
                  string str = "checkOrder(): Error in OrderSelect(" + i + ", SELECT_BY_POS, MODE_HISTORY)";
                  int err_no = mt4.GetLastError();
                  str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
                  Logger.Error(str);
                }
              }
            }
            catch (Exception e)
            {
              string str = "checkOrder(): exception caught";
              int err_no = mt4.GetLastError();
              str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
              str += e;
              Logger.Error(str);
              throw;
            }
            if (!problem) break;
          }
          if (problem) mt4.setEmergencyMode();
        }
      }
      else
      {
        posref = positions[id];
        if (mt4.OrderSelect(id, SelectionType.SELECT_BY_TICKET, SelectionPool.MODE_TRADES))
        {
          int OrderTicket = mt4.OrderTicket();
          int OrderMagicNumber = mt4.OrderMagicNumber();
          string OrderComment = mt4.OrderComment();
          if (Lib.isIntToMagicOk(OrderMagicNumber))
          {
            found = true;
            double OrderClosePrice = mt4.OrderClosePrice();
            DateTime OrderCloseTime = mt4.OrderCloseTime();
            double OrderCommission = mt4.OrderCommission();
            DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
            DateTime zeroTime = OrderExpiration;
            OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
            double OrderLots = mt4.OrderLots();
            double OrderOpenPrice = mt4.OrderOpenPrice();
            DateTime OrderOpenTime = mt4.OrderOpenTime();
            double OrderProfit = mt4.OrderProfit();
            double OrderStopLoss = mt4.OrderStopLoss();
            double OrderSwap = mt4.OrderSwap();
            string OrderSymbol = mt4.OrderSymbol();
            double OrderTakeProfit = mt4.OrderTakeProfit();
            TradeOperation OrderType = mt4.OrderType();
            Enums.PositionStatus status = Enums.PositionStatus.Opened;
            if (OrderType == TradeOperation.OP_BUY || OrderType == TradeOperation.OP_SELL)
            {
              if (OrderCloseTime != zeroTime) status = Enums.PositionStatus.Closed; else status = Enums.PositionStatus.Opened;
            }
            else
            {
              if (OrderCloseTime != zeroTime) status = Enums.PositionStatus.Deleted; else status = Enums.PositionStatus.Pending;
            }

            bool pom = posref.update(status, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, OrderExpiration, OrderLots, OrderMagicNumber, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, OrderSwap, OrderSymbol, OrderTakeProfit, OrderTicket, OrderType);
            if (!pom)
            {
              string str = "checkOrder(): Error in updating " + OrderTicket;
              Logger.Error(str);
            }
          }
        }
        else
        {
          // nie znaleziono pozycji - prawdopodobnie błąd w logice programu
          string str = "checkOrder(): Error in OrderSelect(" + id + ", SELECT_BY_TICKET)";
          int err_no = mt4.GetLastError();
          str += "\nerror " + err_no + ": " + Lib.ErrorDescription(err_no);
          Logger.Error(str);
        }
      }

      updateMargin();

      return found;
    }

    /// <summary>
    /// Otwórz pozycję w strategii aggressive. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="dir">Kierunek pozycji.</param>
    /// <param name="volume">Rozmiar.</param>
    /// <param name="stoploss">Stoploss</param>
    /// <param name="takeprofit">Takeprofit (domyślnie -1 - absurdalnie wysoki).</param>
    /// <param name="slippage">Dopuszczalny poślizg (domyslnie 0).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool openAggressive_vol(Enums.LimitedDirection dir, decimal volume, decimal stoploss, decimal takeprofit = -1, decimal slippage = 0)
    {
      if (dont_open)
      {
        Logger.Info("Can't open aggressive position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage + ": opening lock till " + dont_open_till);
        return false;
      }
      if (nj4x.wymiatacz_fx.Strategy.Marcinek.dont_open_till_end)
      {
        Logger.Info("Can't open aggressive position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage + ": permanent lock");
        return false;
      }
      if (!checkTotalNumOfOpened(Enums.PositionTactics.Aggressive, true))
      {
        Logger.Info("Can't open aggressive position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage + ": too many opened");
        return false;
      }
      bool ret = true;

      TradeOperation oper = TradeOperation.OP_BUY;
      if (dir == Enums.LimitedDirection.Down) oper = TradeOperation.OP_SELL;
      int N_id = id_counter;
      Logger.Info("Opening aggressive position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage);
      Position p = Position.open(Enums.PositionTactics.Aggressive, oper, volume, stoploss, takeprofit, _slippage: slippage, comment: "N: " + N_id);
      id_counter++;
      if (p == null)
      {
        Logger.Error("Failed to open aggressive position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage);
        ret = false;
      }
      else
      {
        if (p.status == Enums.PositionStatus.Unknown)
        {
          Logger.Error("Unknown status of opening aggressive position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage);
          ret = false;
          unknown_positions[N_id] = p;
          System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
          if (checkOrder(N_id, unknown: true))
          {
            ret = true;
          }
          else
          {
            setDontOpen();
          }
        }
        else
        {
          Logger.Info("Opened aggressive position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage + " " + p.id);
          positions[p.id] = p;
          pos_aggressive[p.id] = p;
          System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
          ret = checkOrder(p.id); // miejmy nadzieję, że sleep był długi bo inaczej uznamy porażkę, potem znajdziemy niespodziewane zlecenie i wejdziemy w tryb awaryjny
          id_counter = p.id + 1;
          if (ret) Logger.Info("Confirmed opening aggressive position: " + p.id + " " + p.opening_price + " " + p.opening_time);
        }
        if (ret)
        {
          ChartLogger.LogOpening(mt4, p.id);
        }
      }
      hasAggressive = pos_aggressive.Count() > 0;
      if (ret)
      {
        E.updateE(); // przede wszystkim chcemy uaktualnić free margin
      }
      return ret;
    }

    /// <summary>
    /// Otwórz pozycję w strategii aggressive. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="dir">Kierunek pozycji.</param>
    /// <param name="stoploss">Stoploss</param>
    /// <param name="takeprofit">Takeprofit (domyślnie -1 - absurdalnie wysoki).</param>
    /// <param name="slippage">Dopuszczalny poślizg (domyslnie 0).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool openAggressive(Enums.LimitedDirection dir, decimal stoploss, decimal takeprofit = -1, decimal slippage = 0)
    {
      decimal price = E.ASK;
      if (dir == Enums.LimitedDirection.Down) price = E.BID;
      decimal volume = positionSize(price, stoploss, dir);
      if (volume == 0) return false;
      return openAggressive_vol(dir, volume, stoploss, takeprofit, slippage);
    }

    /// <summary>
    /// Otwórz pozycję w strategii strict. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="dir">Kierunek pozycji.</param>
    /// <param name="price">Cena.</param>
    /// <param name="volume">Rozmiar.</param>
    /// <param name="stoploss">Stoploss</param>
    /// <param name="extr">Pomocnicze ekstremum cenowe w dobrym kierunku.</param>
    /// <param name="extr_bad">Pomocnicze ekstremum cenowe w złym kierunku.</param>
    /// <param name="takeprofit">Takeprofit (domyślnie -1 - absurdalnie wysoki).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool openStrict_vol(Enums.LimitedDirection dir, decimal price, decimal volume, decimal stoploss, decimal extr = -1, decimal extr_bad = -1, decimal takeprofit = -1)
    {
      if (dont_open)
      {
        Logger.Info("Can't open strict position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + ": opening lock till " + dont_open_till);
        return false;
      }
      if (nj4x.wymiatacz_fx.Strategy.Marcinek.dont_open_till_end)
      {
        Logger.Info("Can't open strict position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + ": permanent lock");
        return false;
      }
      if (!checkTotalNumOfOpened(Enums.PositionTactics.Strict, true))
      {
        Logger.Info("Can't open strict position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + ": too many opened");
        return false;
      }
      bool ret = true;

      TradeOperation oper = TradeOperation.OP_BUYLIMIT;
      if (dir == Enums.LimitedDirection.Down) oper = TradeOperation.OP_SELLLIMIT;
      int N_id = id_counter;
      Logger.Info("Opened strict position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + price);
      Position p = Position.open(Enums.PositionTactics.Strict, oper, volume, stoploss, takeprofit, _price: price, comment: "N: " + N_id, _valid_in_minutes: PositionOpeningConfig.pending_valid_bars * minutes_per_bar);
      id_counter++;
      if (p == null)
      {
        Logger.Error("Failed to open strict position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + price);
        ret = false;
      }
      else
      {
        if (dir == Enums.LimitedDirection.Up) p.setImpSRForStrict(imp_SR_for_strict_upper);
        else p.setImpSRForStrict(imp_SR_for_strict_lower);
        p.extremum_plus_strict = extr;
        p.extremum_minus_strict = extr_bad;

        if (p.status == Enums.PositionStatus.Unknown)
        {
          Logger.Error("Unknown status of opening strict position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + price);
          ret = false;
          unknown_positions[N_id] = p;
          System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
          if (checkOrder(N_id, unknown: true))
          {
            ret = true;
          }
          else
          {
            setDontOpen();
          }
        }
        else
        {
          Logger.Info("Opened strict position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + price + " " + p.id);
          positions[p.id] = p;
          pos_strict[p.id] = p;
          System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
          ret = checkOrder(p.id); // miejmy nadzieję, że sleep był długi bo inaczej uznamy porażkę, potem znajdziemy niespodziewane zlecenie i wejdziemy w tryb awaryjny
          id_counter = p.id + 1;
          if (ret) Logger.Info("Confirmed opening strict position: " + p.id + " " + p.opening_price + " " + p.opening_time);
        }
        if (ret)
        {
          ChartLogger.LogOpening(mt4, p.id);
        }
      }
      hasStrict = pos_strict.Count() > 0;
      if (ret)
      {
        E.updateE(); // przede wszystkim chcemy uaktualnić free margin
      }
      return ret;
    }

    /// <summary>
    /// Otwórz pozycję w strategii strict. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="dir">Kierunek pozycji.</param>
    /// <param name="stoploss">Stoploss</param>
    /// <param name="takeprofit">Takeprofit (domyślnie -1 - absurdalnie wysoki).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool openStrict(Enums.LimitedDirection dir, decimal price, decimal stoploss, decimal takeprofit = -1)
    {
      decimal volume = positionSize(price, stoploss, dir);
      if (volume == 0) return false;
      return openStrict_vol(dir, price, volume, stoploss, E.BID, stoploss, takeprofit);
    }

    /// <summary>
    /// Otwórz pozycję w strategii strict. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="dir">Kierunek pozycji.</param>
    /// <param name="stoploss">Stoploss</param>
    /// <param name="extr">Pomocnicze ekstremum cenowe w dobrym kierunku.</param>
    /// <param name="extr_bad">Pomocnicze ekstremum cenowe w złym kierunku.</param>
    /// <param name="takeprofit">Takeprofit (domyślnie -1 - absurdalnie wysoki).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool openStrict(Enums.LimitedDirection dir, decimal price, decimal stoploss, decimal extr, decimal extr_bad, decimal takeprofit = -1)
    {
      decimal volume = positionSize(price, stoploss, dir);
      if (volume == 0) return false;
      return openStrict_vol(dir, price, volume, stoploss, extr, extr_bad, takeprofit);
    }

    /// <summary>
    /// Otwórz pozycję w strategii peak. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="dir">Kierunek pozycji.</param>
    /// <param name="volume">Rozmiar.</param>
    /// <param name="stoploss">Stoploss</param>
    /// <param name="takeprofit">Takeprofit (domyślnie -1 - absurdalnie wysoki).</param>
    /// <param name="slippage">Dopuszczalny poślizg (domyslnie 0).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool openPeak_vol(Enums.LimitedDirection dir, decimal volume, decimal stoploss, decimal takeprofit = -1, decimal slippage = 0)
    {
      if (dont_open)
      {
        Logger.Info("Can't open peak position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage + ": opening lock till " + dont_open_till);
        return false;
      }
      if (nj4x.wymiatacz_fx.Strategy.Marcinek.dont_open_till_end)
      {
        Logger.Info("Can't open peak position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage + ": permanent lock");
        return false;
      }
      if (!checkTotalNumOfOpened(Enums.PositionTactics.Peak, true))
      {
        Logger.Info("Can't open peak position: " + dir + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage + ": too many opened");
        return false;
      }
      bool ret = true;

      TradeOperation oper = TradeOperation.OP_BUY;
      if (dir == Enums.LimitedDirection.Down) oper = TradeOperation.OP_SELL;
      int N_id = id_counter;
      Logger.Info("Opening peak position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage);
      Position p = Position.open(Enums.PositionTactics.Peak, oper, volume, stoploss, takeprofit, _slippage: slippage, comment: "N: " + N_id);
      id_counter++;
      if (p == null)
      {
        Logger.Error("Failed to open peak position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage);
        ret = false;
      }
      else
      {
        if (p.status == Enums.PositionStatus.Unknown)
        {
          Logger.Error("Unknown status of opening peak position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage);
          ret = false;
          unknown_positions[N_id] = p;
          System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
          if (checkOrder(N_id, unknown: true))
          {
            ret = true;
          }
          else
          {
            setDontOpen();
          }
        }
        else
        {
          Logger.Info("Opened peak position: " + N_id + " " + oper + " " + volume + " " + stoploss + " " + takeprofit + " " + slippage + " " + p.id);
          positions[p.id] = p;
          pos_peak[p.id] = p;
          System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
          ret = checkOrder(p.id); // miejmy nadzieję, że sleep był długi bo inaczej uznamy porażkę, potem znajdziemy niespodziewane zlecenie i wejdziemy w tryb awaryjny
          id_counter = p.id + 1;
          if (ret) Logger.Info("Confirmed opening peak position: " + p.id + " " + p.opening_price + " " + p.opening_time);
        }
        lastOpenedPeak = p.id;
        if (ret)
        {
          ChartLogger.LogOpening(mt4, p.id);
        }
      }
      hasPeak = pos_peak.Count() > 0;
      if (ret)
      {
        E.updateE(); // przede wszystkim chcemy uaktualnić free margin
      }
      return ret;
    }

    /// <summary>
    /// Otwórz pozycję w strategii peak. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="dir">Kierunek pozycji.</param>
    /// <param name="stoploss">Stoploss</param>
    /// <param name="takeprofit">Takeprofit (domyślnie -1 - absurdalnie wysoki).</param>
    /// <param name="slippage">Dopuszczalny poślizg (domyslnie 0).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool openPeak(Enums.LimitedDirection dir, decimal stoploss, decimal takeprofit = -1, decimal slippage = 0)
    {
      decimal price = E.ASK;
      if (dir == Enums.LimitedDirection.Down) price = E.BID;
      decimal volume = positionSize(price, stoploss, dir);
      if (volume == 0) return false;
      return openPeak_vol(dir, volume, stoploss, takeprofit, slippage);
    }

    /// <summary>
    /// Modyfikuj oczekującą pozycję. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="ticket">Modyfikowana pozycja.</param>
    /// <param name="_stoploss">Stoploss (domyślnie -1 - bez zmian).</param>
    /// <param name="_takeprofit">Takeprofit (domyślnie -1 - bez zmian).</param>
    /// <param name="_opening_price">Cena otwarcia (domyślnie -1 - bez zmian).</param>
    /// <param name="_valid_in_minutes">Aktualność (w minutach) (domyślnie -1 - bez zmian).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool modifyPending(int ticket, decimal _stoploss = -1, decimal _takeprofit = -1, decimal _opening_price = -1, int _valid_in_minutes = -1)
    {
      bool ret = true;
      Logger.Info("Modifying pending position: " + ticket + " " + _stoploss + " " + _takeprofit + " " + _opening_price + " " + _valid_in_minutes);
      if (pos_strict.ContainsKey(ticket) && pos_strict[ticket].status == Enums.PositionStatus.Pending)
      {
        ret = pos_strict[ticket].modify(_stoploss, _takeprofit, _opening_price, _valid_in_minutes);
        System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
        if (checkOrder(ticket))
        {
          if (positions[ticket].status != Enums.PositionStatus.Pending)
          {
            Logger.Info("modifyPending(): position " + ticket + " changed status from Pending to " + positions[ticket].status);
          }

          if ((_stoploss == -1 || Lib.comp((double)_stoploss, positions[ticket].SL) == 0) && (_takeprofit == -1 || Lib.comp((double)_takeprofit, positions[ticket].TP) == 0) && (_opening_price == -1 || Lib.comp((double)_opening_price, positions[ticket].wanted_opening_price) == 0))
          {
            ret = true;
            Logger.Info("modifyPending(): success for " + ticket + " " + _stoploss + " " + _takeprofit + " " + _opening_price + " " + _valid_in_minutes);
          }
          else
          {
            // tak naprawdę to możemy tu wskoczyć też gdy był timeout przy modyfikacji
            Logger.Error("modifyPending(): failed changing pending parameters for " + ticket + ": SL " + positions[ticket].SL + " instead of " + _stoploss + " TP " + positions[ticket].TP + " instead of " + _takeprofit + " price " + positions[ticket].wanted_opening_price + " instead of " + _opening_price);
          }
        }
        else
        {
          Logger.Error("modifyPending(): error in checkOrder() for ticket " + ticket);
        }
        if (ret)
        {
          ChartLogger.LogModifying(mt4, ticket);
        }
      }
      else
      {
        ret = false;
        Logger.Error("modifyPending(): bad ticket " + ticket);
      }
      hasStrict = pos_strict.Count() > 0;
      return ret;
    }

    /// <summary>
    /// Modyfikuj otwartą pozycję. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="ticket">Modyfikowana pozycja.</param>
    /// <param name="_stoploss">Stoploss (domyślnie -1 - bez zmian).</param>
    /// <param name="_takeprofit">Takeprofit (domyślnie -1 - bez zmian).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool modifyOpened(int ticket, decimal _stoploss = -1, decimal _takeprofit = -1)
    {
      bool ret = true;
      Logger.Info("Modifying opened position: " + ticket + " " + _stoploss + " " + _takeprofit);
      if ((pos_strict.ContainsKey(ticket) || pos_peak.ContainsKey(ticket) || pos_aggressive.ContainsKey(ticket)) && positions[ticket].status == Enums.PositionStatus.Opened)
      {
        TimeSpan ts = E.TIME - positions[ticket].last_modification_date;
        if (ts.TotalMilliseconds < PositionModifyingConfig.OpenedPositionModifyingDelay)
        {
          Logger.Info("Couldn't modify opened position: " + ticket + " " + _stoploss + " " + _takeprofit + " due to delay required");
          return false;
        }

        ret = positions[ticket].modify(_stoploss, _takeprofit);
        System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
        if (checkOrder(ticket))
        {
          if ((_stoploss == -1 || Lib.comp((double)_stoploss, positions[ticket].SL) == 0) && (_takeprofit == -1 || Lib.comp((double)_takeprofit, positions[ticket].TP) == 0))
          {
            ret = true;
            Logger.Info("modifyOpened(): success for " + ticket + " " + _stoploss + " " + _takeprofit);
          }
          else
          {
            Logger.Error("modifyOpened(): failed changing pending parameters for " + ticket + ": SL " + positions[ticket].SL + " instead of " + _stoploss + " TP " + positions[ticket].TP + " instead of " + _takeprofit);
          }
        }
        else
        {
          Logger.Error("modifyOpened(): error in checkOrder() for ticket " + ticket);
        }
        if (ret)
        {
          if (!LocalConfig.OFFLINE_TESTING)
          {
            // przy testach online tworzenie procesów logujących w takich ilościach zapycha system (out of memory exception!)
#pragma warning disable 0162
            ChartLogger.LogModifying(mt4, ticket);
#pragma warning restore 0162
          }
        }
      }
      else
      {
        ret = false;
        Logger.Error("modifyOpened(): bad ticket " + ticket);
      }
      hasAggressive = pos_aggressive.Count() > 0;
      hasPeak = pos_peak.Count() > 0;
      hasStrict = pos_strict.Count() > 0;
      return ret;
    }

    /// <summary>
    /// Usuń oczekującą pozycję. Zwraca czy się udało (gdy nie wiemy, zwraca false).
    /// </summary>
    /// <param name="ticket">Usuwana zamykana.</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool deletePending(int ticket)
    {
      bool ret = true;
      Logger.Info("Deleting pending position: " + ticket);
      if (pos_strict.ContainsKey(ticket) && pos_strict[ticket].status == Enums.PositionStatus.Pending)
      {
        ret = pos_strict[ticket].delete();
        System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
        if (checkOrder(ticket))
        {
          if (positions[ticket].status == Enums.PositionStatus.Deleted)
          {
            ret = true;
            pos_strict.Remove(ticket);
            Logger.Info("deletePending(): success for " + ticket);
          }
          else
          {
            Logger.Error("deletePending(): status for " + ticket + ": " + positions[ticket].status);
          }
        }
        else
        {
          Logger.Error("deletePending(): error in checkOrder() for ticket " + ticket);
        }
        if (ret)
        {
          ChartLogger.LogDeleting(mt4, ticket);
        }
      }
      else
      {
        ret = false;
        Logger.Error("deletePending(): bad ticket " + ticket);
      }
      hasStrict = pos_strict.Count() > 0;
      if (ret) E.updateE(); // przede wszystkim chcemy uaktualnić free margin
      return ret;
    }

    /// <summary>
    /// Zamknij pozycję. Zwraca czy się udało (gdy nie wiemy, zwraca false). Może uaktualnić unbound_predecessors.
    /// </summary>
    /// <param name="ticket">Pozycja zamykana.</param>
    /// <param name="slippage">Dopuszczalny poślizg cenowy (domyślnie 0).</param>
    /// <param name="volume">Rozmiar zamknięcia *domyślnie -1 - całość pozycji).</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool closeOpened(int ticket, decimal slippage = 0, decimal volume = -1)
    {
      bool ret = true;
      Logger.Info("Closing opened position: " + ticket + " " + slippage + " " + volume);
      if ((pos_strict.ContainsKey(ticket) || pos_peak.ContainsKey(ticket) || pos_aggressive.ContainsKey(ticket)) && positions[ticket].status == Enums.PositionStatus.Opened)
      {
        bool partial = false;
        if (volume != -1 && Lib.comp((double)volume, positions[ticket].volume) < 0) partial = true;
        decimal resid = (decimal)positions[ticket].volume - volume;
        ret = positions[ticket].close(slippage, volume);
        if (!ret)
        {
          Logger.Error("closeOpened(): problems in closing ticket " + ticket);
        }
        System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
        if ((checkOrder(ticket) && positions[ticket].status == Enums.PositionStatus.Closed) || ret)
        {
          ret = true;
          pos_strict.Remove(ticket);
          pos_aggressive.Remove(ticket);
          pos_peak.Remove(ticket);
          Logger.Info("closeOpened(): success for " + ticket);
          if (partial)
          {
            unbound_predecessors[ticket] = Position.normalize(resid);
            accounting();
          }
        }
        else
        {
          Logger.Error("closeOpened(): problems in closing ticket " + ticket + ": status is " + positions[ticket].status);
        }
        // jeśli do tej pory nie wiemy czy się udało, to zakładamy że nie i w razie czego będzie tryb awaryjny gdy wykryjemy coś nowego przy partial

        if (ret)
        {
          ChartLogger.LogClosing(mt4, ticket);
        }
      }
      else
      {
        ret = false;
        Logger.Error("closeOpened(): bad ticket " + ticket);
      }
      hasAggressive = pos_aggressive.Count() > 0;
      hasPeak = pos_peak.Count() > 0;
      hasStrict = pos_strict.Count() > 0;
      if (ret) E.updateE(); // przede wszystkim chcemy uaktualnić free margin
      return ret;
    }

    /// <summary>
    /// Zamknij pozycję drugą (przeciwną) pozycją. Zwraca czy się udało (gdy nie wiemy, zwraca false). Może uaktualnić unbound_predecessors.
    /// </summary>
    /// <param name="ticket">Pozycja zamykana.</param>
    /// <param name="complementary">Pozycja zamykająca.</param>
    /// <returns>Zwraca czy się udało (gdy nie wiemy, zwraca false).</returns>
    public bool closeOpenedWith(int ticket, int complementary)
    {
      bool ret = true;
      // to jest konieczne bo jest ważne co jest pozycją zamykaną a co zamykającą
      // MT4 jest spierdolony: gdy większą zamykamy mniejszą to obie są zamykane i pojawia się nowa pozycja z komentarzem "partial close" - mamy problem z dopasowaniem jej do akcji (trzeba by identyfikować po magic number)
      // gdy mniejszą zamykamy większą, nowa pozycja ma komentarz "split from #x" gdzie x to id pozycji zamykanej
      if (positions[ticket].volume > positions[complementary].volume) Lib.Swap(ref ticket, ref complementary);

      Logger.Info("Closing opened position: " + ticket + " with " + complementary);
      if ((pos_strict.ContainsKey(ticket) || pos_peak.ContainsKey(ticket) || pos_aggressive.ContainsKey(ticket)) && (pos_strict.ContainsKey(complementary) || pos_peak.ContainsKey(complementary) || pos_aggressive.ContainsKey(complementary)))
      {
        bool ok = true;
        if (positions[ticket].status != Enums.PositionStatus.Opened)
        {
          ok = false;
          ret = false;
          Logger.Error("closeOpened(): bad status for ticket " + ticket + ": " + positions[ticket].status);
        }
        if (positions[complementary].status != Enums.PositionStatus.Opened)
        {
          ok = false;
          ret = false;
          Logger.Error("closeOpened(): bad status for ticket " + complementary + ": " + positions[ticket].status);
        }
        if (ok)
        {
          bool partial = false;
          if (Lib.comp(positions[ticket].volume, positions[complementary].volume) != 0) partial = true;
          decimal resid = (decimal)positions[ticket].volume - (decimal)positions[complementary].volume;
          ret = positions[ticket].close(positions[complementary]);
          //Console.WriteLine("YOB: " + testPositionsListing());
          if (!ret)
          {
            Logger.Error("closeOpenedWith(): problems in closing ticket " + ticket);
          }
          System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
          if ((checkOrder(ticket) && positions[ticket].status == Enums.PositionStatus.Closed && checkOrder(complementary) && positions[complementary].status == Enums.PositionStatus.Closed) || ret)
          {
            ret = true;
            pos_strict.Remove(ticket);
            pos_aggressive.Remove(ticket);
            pos_peak.Remove(ticket);
            pos_strict.Remove(complementary);
            pos_aggressive.Remove(complementary);
            pos_peak.Remove(complementary);
            Logger.Info("closeOpenedWith(): success for " + ticket + " and " + complementary);
            if (partial)
            {
              if (resid < 0)
              {
                unbound_predecessors[complementary] = Position.normalize(-resid);
              }
              else
              {
                unbound_predecessors[ticket] = Position.normalize(resid);
              }
              accounting();
            }
          }
          else
          {
            Logger.Error("closeOpenedWith(): problems in closing ticket " + ticket + " (status is " + positions[ticket].status + ") with " + complementary + " (status is " + positions[complementary].status + ")");
          }
          // jeśli do tej pory nie wiemy czy się udało, to zakładamy że nie i w razie czego będzie tryb awaryjny gdy wykryjemy coś nowego przy partial

          if (ret)
          {
            ChartLogger.LogClosing(mt4, ticket);
            ChartLogger.LogClosing(mt4, complementary);
          }
        }
      }
      else
      {
        ret = false;
        Logger.Error("closeOpened(): bad ticket " + ticket + " or " + complementary);
      }
      hasAggressive = pos_aggressive.Count() > 0;
      hasPeak = pos_peak.Count() > 0;
      hasStrict = pos_strict.Count() > 0;
      if (ret) E.updateE(); // przede wszystkim chcemy uaktualnić free margin
      return ret;
    }

    /// <summary>
    /// Wyciśnij pozycję (także ściśle). Zwraca czy zmodyfikowano parametry pozycji.
    /// </summary>
    /// <param name="ticket">Numer pozycji.</param>
    /// <returns>Czy zmodyfikowano parametry pozycji.</returns>
    public bool squeeze(int ticket)
    {
      bool ret = false;
      if ((pos_strict.ContainsKey(ticket) || pos_peak.ContainsKey(ticket) || pos_aggressive.ContainsKey(ticket)) && positions[ticket].status == Enums.PositionStatus.Opened)
      {
        double _sl1 = (double)(positions[ticket].getSL_lim());
        double _tp1 = (double)(positions[ticket].getTP_lim());
        if (positions[ticket].squeeze())
        {
          // złożono zlecenie
          System.Threading.Thread.Sleep(LocalConfig.DELAY_AFTER_POSITION_ORDER);
          if (checkOrder(ticket))
          {
            if (Lib.comp(_sl1, positions[ticket].SL) == 0 && Lib.comp(_tp1, positions[ticket].TP) == 0)
            {
              // success
              ret = true;
            }
            else
            {
              Logger.Error("squeeze(): failed for " + ticket + ": SL " + positions[ticket].SL + " instead of " + _sl1 + " TP " + positions[ticket].TP + " instead of " + _tp1);
            }
          }
          else
          {
            Logger.Error("squeeze(): error in checkOrder() for ticket " + ticket);
          }
        }
      }
      else
      {
        Logger.Error("squeeze(): bad ticket " + ticket);
      }
      hasAggressive = pos_aggressive.Count() > 0;
      hasPeak = pos_peak.Count() > 0;
      hasStrict = pos_strict.Count() > 0;
      return ret;
    }

    /// <summary>
    /// Sprawdza czy można otworzyć nową pozycję (licząc już otwarte).
    /// </summary>
    /// <param name="t">Type nowej pozycji.</param>
    /// <param name="print">Czy wypisywać uzasadnienie wyniku false (domyślnie: nie).</param>
    /// <returns>Czy można otworzyć nową pozycję (licząc już otwarte).</returns>
    private bool checkTotalNumOfOpened(Enums.PositionTactics t, bool print = false)
    {
      string str = "";
      bool ret = true;
      int s = pos_strict.Count();
      int a = pos_aggressive.Count();
      int p = pos_peak.Count();
      if (s > 1)
      {
        str += "" + s + " strict positions! ";
        ret = false;
      }
      if (a > 1)
      {
        str += "" + a + " aggressive positions! ";
        ret = false;
      }
      if (p > 1)
      {
        str += "" + p + " peak positions! ";
        ret = false;
      }
      switch (t)
      {
        case Enums.PositionTactics.Strict:
          if (s > 0)
          {
            str += " Strict already opened! ";
            ret = false;
          }
          break;
        case Enums.PositionTactics.Aggressive:
          if (a > 0)
          {
            str += " Aggressive already opened! ";
            ret = false;
          }
          break;
        case Enums.PositionTactics.Peak:
          if (p > 0)
          {
            str += " Peak already opened! ";
            ret = false;
          }
          break;
      }
      if (print && !ret)
      {
        Logger.Info("checkTotalNumOfOpened(" + t + "): " + str);
      }
      return ret;
    }

    /// <summary>
    /// Uaktualnij (zrób księgowanie i obsłuż wyciskanie).
    /// </summary>
    public void update()
    {
      if (dont_open && DateTime.UtcNow > dont_open_till)
      {
        Logger.Info("Releasing opening lock");
        dont_open = false;
      }
      accounting();
      hasAggressive = pos_aggressive.Count() > 0;
      hasPeak = pos_peak.Count() > 0;
      hasStrict = pos_strict.Count() > 0;
      foreach (var k in pos_peak.Keys.ToList())
      {
        var x = positions[k];
        if (x.beingSqeezed)
        {
          if (!x.beingTightlySqeezed)
          {
            // trwa wyciskanie
            if (!x.worthSqueezingFurther())
            {
              x.setToSqueezeTightly();
            }
          }
          if (x.worthSqueezingFurther() && x.worthTightlySqueezingFurther()) squeeze(x.id);
        }
      }
      foreach (var k in pos_aggressive.Keys.ToList())
      {
        var x = positions[k];
        if (x.beingSqeezed)
        {
          if (!x.beingTightlySqeezed)
          {
            // trwa wyciskanie
            if (!x.worthSqueezingFurther())
            {
              x.setToSqueezeTightly();
            }
          }
          if (x.worthSqueezingFurther() && x.worthTightlySqueezingFurther()) squeeze(x.id);
        }
      }
      foreach (var k in pos_strict.Keys.ToList())
      {
        var x = positions[k];
        if (x.beingSqeezed)
        {
          if (!x.beingTightlySqeezed)
          {
            // trwa wyciskanie
            if (!x.worthSqueezingFurther())
            {
              x.setToSqueezeTightly();
            }
          }
          if (x.worthSqueezingFurther() && x.worthTightlySqueezingFurther()) squeeze(x.id);
        }
      }

      updateMargin();
    }

    /// <summary>
    /// Uaktualnia informacje o zasobach konta, m.in. free_margin (pesymistyczna ocena wolnego depozytu), used_free_margin_mc_new (pesymistyczny ubytek z depozytu po otwarciu nowej pozycji o rozmiarze 1 lota; uwzględniamy przesunięcie z wolnych środków do zaalokowanych i ubytek w przypadku straty zaalokowanych środków z zapasem 10%), allowed_new_lots (maksymalny wolumen w lotach, jaki jeszcze możemy zaangażować bez znaczącego ryzyka naruszenia poziomów stopout lub margin call).
    /// </summary>
    private void updateMargin()
    {
      if (E.AccountStopoutMode == 0)
      {
        stopout = (PositionOpeningConfig.stopout_fold + ((decimal)E.AccountStopoutLevel / 100)) * (decimal)E.AccountEquity;
      }
      else
      {
        stopout = PositionOpeningConfig.stopout_fold * (decimal)E.AccountEquity + (decimal)E.AccountStopoutLevel;
      }

      total_positions_volume = 0;
      free_margin = (decimal)E.AccountFreeMargin;
      // uwzględniamy pesymistyczne obsuwy i zamknięcia na S/L otwartych i oczekujących pozycji
      // przy otwieraniu pozycji także będziemy musieli wziąć pod uwagę pesymistyczne zamknięcie na S/L - chodzi o to, żeby w żadnym wypadku nie przekroczyć poziomu stopout
      if (E.AccountFreeMarginMode == 0 || E.AccountFreeMarginMode == 2)
      {
        // uwzględniamy ewentualną stratę od ceny otwarcia do S/L; dodajemy poślizg
        foreach (var k in pos_peak.Keys.ToList())
        {
          var x = positions[k];
          free_margin -= x.maxLossFromOpening;
          total_positions_volume += (decimal)x.volume;
        }
        foreach (var k in pos_aggressive.Keys.ToList())
        {
          var x = positions[k];
          free_margin -= x.maxLossFromOpening;
          total_positions_volume += (decimal)x.volume;
        }
        foreach (var k in pos_strict.Keys.ToList())
        {
          var x = positions[k];
          free_margin -= x.maxLossFromOpening;
          total_positions_volume += (decimal)x.volume;
        }
        foreach (var k in unknown_positions.Keys.ToList())
        {
          var x = unknown_positions[k]; // pozycji z unknown_positions jeszcze nie ma w positions
          free_margin -= x.maxLossFromOpening;
          total_positions_volume += (decimal)x.volume;
        }
      }
      else
      {
        // uwzględniamy ewentualną stratę od ceny bieżącej do S/L; dodajemy poślizg
        foreach (var k in pos_peak.Keys.ToList())
        {
          var x = positions[k];
          free_margin -= x.maxLossFromCurrent;
          total_positions_volume += (decimal)x.volume;
        }
        foreach (var k in pos_aggressive.Keys.ToList())
        {
          var x = positions[k];
          free_margin -= x.maxLossFromCurrent;
          total_positions_volume += (decimal)x.volume;
        }
        foreach (var k in pos_strict.Keys.ToList())
        {
          var x = positions[k];
          free_margin -= x.maxLossFromCurrent;
          total_positions_volume += (decimal)x.volume;
        }
        foreach (var k in unknown_positions.Keys.ToList())
        {
          var x = unknown_positions[k]; // pozycji z unknown_positions jeszcze nie ma w positions
          free_margin -= x.maxLossFromCurrent;
          total_positions_volume += (decimal)x.volume;
        }
      }

      if (stopout >= (1 - PositionOpeningConfig.margin_call_fold) * free_margin)
      {
        string str = "STOPOUT WARNING at " + E.TIME + " because stopout level " + stopout + " required but only " + free_margin + " available!";
        if (!stopout_warning_issued)
        {
          stopout_warning_issued = true;
          str += "\n" + accountingInfo() + "\n" + positionsXML(evolution: true);
        }
        Logger.Error(str);
        // TODO: jeśli kiedyś to się pojawi, to może coś dalej tu zrobić (zamykanie pozycji, tryb awaryjny?)
        // BARDZO nie chcemy stopoutu, bo wtedy broker np. zamyka pozycje wg. własnego uznania lub może zablokować rachunek!
      }
      if (free_margin < 0) free_margin = 0;
      usable_free_margin = free_margin - stopout;
      if (usable_free_margin < 0) usable_free_margin = 0;

      remaining_free_margin = free_margin;

      used_free_margin_mc_current = E.MARGINMAINTENANCE;
      used_free_margin_mc_new = E.MARGININIT;
      if (used_free_margin_mc_current > E.MARGININIT) used_free_margin_mc_new = used_free_margin_mc_current;
      decimal pom = (E.BID + E.ASK) * E.LOTSIZE / (2 * E.AccountLeverage);
      if (pom > E.MARGINREQUIRED)
      {
        used_free_margin_mc_new += pom;
      }
      else
      {
        used_free_margin_mc_new += E.MARGINREQUIRED;
      }
      if (E.MARGINHEDGED > E.LOTSIZE)
      {
        // trochę na wyrost - zakładamy hedge'owanie wszystkiego
        used_free_margin_mc_current *= E.MARGINHEDGED / E.LOTSIZE;
        used_free_margin_mc_new *= E.MARGINHEDGED / E.LOTSIZE;
      }
      used_free_margin_mc_current *= total_positions_volume; // equity nie może spaść poniżej tej wartości bo inaczej będzie margin call!
      // robimy z pesymistycznym zapasem - porównujemy z wolnym ( w pesymistycznem przypadku) depozytem z marginesem bezpieczeństwa
      decimal fm = (1 - PositionOpeningConfig.margin_call_fold) * free_margin;
      if (used_free_margin_mc_current >= fm)
      {
        remaining_free_margin = 0;
        string str = "MARGIN CALL WARNING at " + E.TIME + " because margin " + used_free_margin_mc_current + " wanted but only " + free_margin + " available!";
        if (!margin_call_warning_issued)
        {
          margin_call_warning_issued = true;
          str += "\n" + accountingInfo() + "\n" + positionsXML(evolution: true);
        }
        Logger.Error(str);
        // TODO: jeśli kiedyś to się pojawi, to może coś dalej tu zrobić (zamykanie pozycji, tryb awaryjny?)
        // BARDZO nie chcemy margin call, bo wtedy broker np. zamyka pozycje wg. własnego uznania lub może zablokować rachunek!
      }
      else
      {
        remaining_free_margin -= used_free_margin_mc_current;
      }

      fm = remaining_free_margin;
      if (remaining_free_margin > usable_free_margin) fm = usable_free_margin;
      if (fm < 0) fm = 0;

      // Pesymistycznie mały maksymalny rozmiar pozycji do otwarcia (biorąc pod uwagę ograniczenia z margin call):
      // (remaining_free_margin-margin_lost_when_new_position_lost) / used_free_margin_mc_new=max_size_of_the_new_position
      // równoważnie:
      // (remaining_free_margin-x*used_free_margin_mc_new*1.1) / used_free_margin_mc_new = x
      // gdzie x:=allowed_new_lots.
      // Więc zamiast:
      // allowed_new_lots = remaining_free_margin / used_free_margin_mc_new;
      // mamy:
      allowed_new_lots = fm / (2.1M * used_free_margin_mc_new);
      // TODO: tak naprawdę w powyższym trzeba by dodać pesymistyczne poślizgi a nie zakładać, że strata może wynieść 110% kapitału na pozycję ale takie uproszczenie powinno być ok
      if (Lib.comp(E.MAXLOT, allowed_new_lots) == -1) allowed_new_lots = E.MAXLOT;

      /*if (E.tick_counter == 6650 || E.tick_counter == 6651 || E.tick_counter == 7900 || E.tick_counter == 8779 || E.tick_counter == 14123 || E.tick_counter == 15399 || E.tick_counter == 15400 || E.tick_counter == 15401)
      {
        Console.WriteLine("KURWA: " + stopout + " " + total_positions_volume + " " + free_margin + " " + usable_free_margin + " " + used_free_margin_mc_current + " " + used_free_margin_mc_new + " " + remaining_free_margin + " " + allowed_new_lots);
      }*/
    }

    /// <summary>
    /// Zwróć rozmiar pozycji, jaką można otworzyć (w lotach). Jeśli 0, to nie można otworzyć.
    /// </summary>
    /// <param name="price">Cena otwarcia pozycji.</param>
    /// <param name="SL">Poziom SL.</param>
    /// <param name="dir">Kierunek pozycji.</param>
    /// <returns>Rozmiar pozycji, jaką można otworzyć (w lotach). Jeśli 0, to nie można otworzyć.</returns>
    private decimal positionSize(decimal price, decimal SL, Enums.LimitedDirection dir)
    {
      decimal ret = 0;
      if (Lib.comp(price, SL) == 0) return 0;

      decimal risk = price - SL;
      if (risk < 0) risk = -risk;
      risk += 2 * E.getSlippage(histery_on_market: true);
      // teraz risk to ryzyko cenowe (maksymalny niekorzystny ruch cen)

      decimal money = 0;
#pragma warning disable 0162
      switch (PositionOpeningConfig.MoneyManager)
      {
        case Enums.CapitalManagementStrategy.ConstantRisk:
          money = PositionOpeningConfig.ConstantRisk_capital_unit;
          if (Lib.comp(money, free_margin) == 1) money = 0;
          break;
        case Enums.CapitalManagementStrategy.PercentageRisk:
          money = (decimal)E.AccountEquity;
          if ((decimal)E.AccountBalance < money) money = (decimal)E.AccountBalance;
          money *= PositionOpeningConfig.PercentageRisk_fraction;
          if (free_margin < money) money = free_margin;
          break;
        case Enums.CapitalManagementStrategy.KellysCriterion:
          break;
      }
#pragma warning restore 0162
      if (money == 0) return 0;
      // teraz money to ilość pieniędzy, jaką ryzykujemy 

      decimal av_tick_val = E.TICKVALUE;
      // to jest pesymistyczna poprawka na wartość pipsa uwzględniająca niekorzystne zmiany kursów
      decimal poprawka = 1; // waluta depozytowa jest jednocześnie walutą kwotowaną
#pragma warning disable 0162
      switch (InitConfig.DepositCurrency)
      {
        case Enums.CurrencyType.Base:
          // waluta depozytowa jest jednocześnie walutą bazową
          poprawka = (E.BID + E.ASK) / 2;
          poprawka = risk / poprawka;
          poprawka = 1 + poprawka; // tak naprawdę mogłoby być (1 + param / 2) bo to właśnie jest pesymistyczna średnia ale dmuchamy na zimne...
          break;
        case Enums.CurrencyType.Deposit:
          // waluta depozytowa nie jest walutą ani bazową ani kwotowaną
          poprawka = (E.BID + E.ASK) / 2;
          poprawka = risk / poprawka;
          poprawka = 1 + poprawka; // tak naprawdę mogłoby być (1 + param / 2) bo to właśnie jest pesymistyczna średnia ale dmuchamy na zimne...
          poprawka *= poprawka;
          break;
      }
#pragma warning restore 0162
      av_tick_val *= poprawka;

      string str = "positionSize(" + price + ", " + SL + "): risk: " + risk + " money: " + money + " av_tick_val: " + av_tick_val + " max: " + allowed_new_lots;
      risk /= E.TICKSIZE;
      risk *= av_tick_val;
      // teraz risk to maksymalna strata gdy otworzymy pozycję 1 lot

      ret = money / risk;
      // sugerowany rozmiar pozycji w lotach
      if (LogConfig.LOG_POSITION_SIZE_CALC)
      {
#pragma warning disable 0162
        str += " m_risk: " + risk + " ret: " + ret;
        Console.WriteLine(str);
#pragma warning restore 0162
      }


      if (Lib.comp(allowed_new_lots, ret) == -1) ret = allowed_new_lots;
      ret = (decimal)Position.normalize(ret, E.LOTSTEP);
      if (Lib.comp(E.MINLOT, ret) == 1) ret = 0;

      TradeOperation cmd = TradeOperation.OP_BUY;
      if (dir == Enums.LimitedDirection.Down) cmd = TradeOperation.OP_SELL;
      decimal nfm = E.AccountFreeMarginCheck(cmd, ret);
      decimal delta = (decimal)E.AccountFreeMargin - nfm;
      //Console.WriteLine("KURWA: " + nfm + " " + delta);
      if (Lib.comp(delta, free_margin) == 1)
      {
        // to się nie powinno wydarzyć
        // TODO: może raczej przejść w tryb awaryjny (i zwrócić z funkcji 0)?
        str = "ERROR in positionSize(): calculated volume " + ret + " leaves to small free margin: " + nfm + " (delta: " + delta + ")!";
        ret *= 0.9M * free_margin / delta;
        ret = (decimal)Position.normalize(ret, E.LOTSTEP);
        if (Lib.comp(E.MINLOT, ret) == 1) ret = 0;
        str += " Updated volume: " + ret;
        Console.WriteLine(str);
      }

      return ret;
    }

    /// <summary>
    /// Zwróć XML opisujący pozycje.
    /// </summary>
    /// <param name="include_historical">Czy zawrzeć usunięte/zamknięte.</param>
    /// <param name="evolution">Czy wypisywać też zmiany poziomów S/L i T/P.</param>
    /// <returns>XML opisujący pozycje.</returns>
    public string positionsXML(bool include_historical = false, bool evolution = false)
    {
      string ret = "<positions>\n";
      if (pos_aggressive.Count() == 0)
      {
        ret += "\t<aggressive count=\"0\"/>\n";
      }
      else
      {
        ret += "\t<aggressive count=\"" + pos_aggressive.Count() + "\">\n";
        int counter = 0;
        foreach (var x in pos_aggressive.Keys)
        {
          ret += "\t\t<item counter=\"" + counter + "\" id=\"" + x + "\"/>\n";
          counter++;
        }
        ret += "\t</aggressive>\n";
      }
      if (pos_peak.Count() == 0)
      {
        ret += "\t<peak count=\"0\"/>\n";
      }
      else
      {
        ret += "\t<peak count=\"" + pos_peak.Count() + "\">\n";
        int counter = 0;
        foreach (var x in pos_peak.Keys)
        {
          ret += "\t\t<item counter=\"" + counter + "\" id=\"" + x + "\"/>\n";
          counter++;
        }
        ret += "\t</peak>\n";
      }
      if (pos_strict.Count() == 0)
      {
        ret += "\t<strict count=\"0\"/>\n";
      }
      else
      {
        ret += "\t<strict count=\"" + pos_strict.Count() + "\">\n";
        int counter = 0;
        foreach (var x in pos_strict.Keys)
        {
          ret += "\t\t<item counter=\"" + counter + "\" id=\"" + x + "\"/>\n";
          counter++;
        }
        ret += "\t</strict>\n";
      }

      if (unbound_predecessors.Count() == 0)
      {
        ret += "\t<unbound count=\"0\"/>\n";
      }
      else
      {
        ret += "\t<unbound count=\"" + unbound_predecessors.Count() + "\">\n";
        int counter = 0;
        foreach (var x in unbound_predecessors.Keys)
        {
          ret += "\t\t<item counter=\"" + counter + "\" pred_id=\"" + x + "\" volume=\"" + unbound_predecessors[x] + "\"/>\n";
          counter++;
        }
        ret += "\t</unbound>\n";
      }

      if (unknown_positions.Count() == 0)
      {
        ret += "\t<unknown count=\"0\"/>\n";
      }
      else
      {
        ret += "\t<unknown count=\"" + unknown_positions.Count() + "\">\n";
        int counter = 0;
        foreach (var x in unknown_positions.Keys)
        {
          ret += "\t\t<item counter=\"" + counter + "\" temp_id=\"" + x + "\">\n";
          ret += Lib.indent(unknown_positions[x].getXML(evolution), "\t\t\t") + "\n";
          ret += "\t\t</item>\n";
          counter++;
        }
        ret += "\t</unknown>\n";
      }

      int act_c = pos_aggressive.Count() + pos_peak.Count() + pos_strict.Count();
      int all_c = positions.Count();
      int hist_c = all_c - act_c;
      int c = include_historical ? all_c : act_c;
      if (c == 0)
      {
        ret += "\t<trading active_count=\"" + act_c + "\" historical_count=\"" + hist_c + "\" total_count=\"" + all_c + "\" include_historical=\"" + include_historical + "\"/>\n";
      }
      else
      {

        ret += "\t<trading active_count=\"" + act_c + "\" historical_count=\"" + hist_c + "\" total_count=\"" + all_c + "\" include_historical=\"" + include_historical + "\">\n";
        int counter = 0;
        foreach (var x in positions.Keys)
        {
          if (!include_historical && (positions[x].status == Enums.PositionStatus.Closed || positions[x].status == Enums.PositionStatus.Deleted)) continue;
          ret += "\t\t<item counter=\"" + counter + "\">\n";
          ret += Lib.indent(positions[x].getXML(evolution), "\t\t\t") + "\n";
          ret += "\t\t</item>\n";
          counter++;
        }
        ret += "\t</trading>\n";
      }
      ret += "</positions>";

      return ret;
    }

    /// <summary>
    /// Napis debugowy: pozycje w MT4.
    /// </summary>
    /// <returns>Pozycje z MT4.</returns>
    /// <exception cref="Exception">Coś poszło nie tak i musimy kończyć program.</exception>
    public string testPositionsListing()
    {
      string str = "ACTIVE:\n";
      for (int i = mt4.OrdersTotal() - 1; i >= 0; i--)
      {
        if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_TRADES))
        {
          int OrderTicket = mt4.OrderTicket();
          int OrderMagicNumber = mt4.OrderMagicNumber();
          double OrderClosePrice = mt4.OrderClosePrice();
          DateTime OrderCloseTime = mt4.OrderCloseTime();
          string OrderComment = mt4.OrderComment();
          double OrderCommission = mt4.OrderCommission();
          DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
          OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
          double OrderLots = mt4.OrderLots();
          double OrderOpenPrice = mt4.OrderOpenPrice();
          DateTime OrderOpenTime = mt4.OrderOpenTime();
          double OrderProfit = mt4.OrderProfit();
          double OrderStopLoss = mt4.OrderStopLoss();
          double OrderSwap = mt4.OrderSwap();
          string OrderSymbol = mt4.OrderSymbol();
          double OrderTakeProfit = mt4.OrderTakeProfit();
          TradeOperation OrderType = mt4.OrderType();
          str += OrderSymbol + ": " + OrderTicket + " " + OrderType + " " + OrderMagicNumber + " " + OrderOpenPrice + " " + OrderOpenTime + " " + OrderLots + " " + OrderStopLoss + " " + OrderTakeProfit + " " + OrderCommission + " " + OrderSwap + " " + OrderProfit + " " + OrderExpiration + " " + OrderClosePrice + " " + OrderCloseTime + " " + OrderComment + "\n";
        }
      }
      str += "HISTORY:\n";
      for (int i = mt4.OrdersHistoryTotal() - 1; i >= 0; i--)
      {
        if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_HISTORY))
        {
          int OrderTicket = mt4.OrderTicket();
          int OrderMagicNumber = mt4.OrderMagicNumber();
          double OrderClosePrice = mt4.OrderClosePrice();
          DateTime OrderCloseTime = mt4.OrderCloseTime();
          string OrderComment = mt4.OrderComment();
          double OrderCommission = mt4.OrderCommission();
          DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
          OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
          double OrderLots = mt4.OrderLots();
          double OrderOpenPrice = mt4.OrderOpenPrice();
          DateTime OrderOpenTime = mt4.OrderOpenTime();
          double OrderProfit = mt4.OrderProfit();
          double OrderStopLoss = mt4.OrderStopLoss();
          double OrderSwap = mt4.OrderSwap();
          string OrderSymbol = mt4.OrderSymbol();
          double OrderTakeProfit = mt4.OrderTakeProfit();
          TradeOperation OrderType = mt4.OrderType();
          str += OrderSymbol + ": " + OrderTicket + " " + OrderType + " " + OrderMagicNumber + " " + OrderOpenPrice + " " + OrderOpenTime + " " + OrderLots + " " + OrderStopLoss + " " + OrderTakeProfit + " " + OrderCommission + " " + OrderSwap + " " + OrderProfit + " " + OrderExpiration + " " + OrderClosePrice + " " + OrderCloseTime + " " + OrderComment + "\n";
        }
      }
      return str;
    }


    /// <summary>
    /// Wypisuje pozycje w MT4. Zwraca parę: czy znaleziono jakieś pozycje aktywne należące do robota (rozpoznaje po magic number), czy w ogóle znaleziono jakieś aktywne pozycje.
    /// </summary>
    /// <returns>Para: czy znaleziono jakieś pozycje aktywne należące do robota (rozpoznaje po magic number), czy w ogóle znaleziono jakieś aktywne pozycje.</returns>
    /// <exception cref="Exception">Coś poszło nie tak i musimy kończyć program.</exception>
    public Tuple<bool, bool> rawPositionsListing()
    {
      string rob = "";
      string norob = "";
      bool ret1 = false;
      bool ret2 = false;
      mt4.GetLastError(); // wyczyść bufor błędu
      string str = "ACTIVE - ROBOT:\n";
      for (int i = mt4.OrdersTotal() - 1; i >= 0; i--)
      {
        if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_TRADES))
        {
          int OrderTicket = mt4.OrderTicket();
          int OrderMagicNumber = mt4.OrderMagicNumber();
          double OrderClosePrice = mt4.OrderClosePrice();
          DateTime OrderCloseTime = mt4.OrderCloseTime();
          string OrderComment = mt4.OrderComment();
          double OrderCommission = mt4.OrderCommission();
          DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
          OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
          double OrderLots = mt4.OrderLots();
          double OrderOpenPrice = mt4.OrderOpenPrice();
          DateTime OrderOpenTime = mt4.OrderOpenTime();
          double OrderProfit = mt4.OrderProfit();
          double OrderStopLoss = mt4.OrderStopLoss();
          double OrderSwap = mt4.OrderSwap();
          string OrderSymbol = mt4.OrderSymbol();
          double OrderTakeProfit = mt4.OrderTakeProfit();
          TradeOperation OrderType = mt4.OrderType();
          if (Lib.isIntToMagicOk(OrderMagicNumber))
          {
            rob += OrderSymbol + ": " + OrderTicket + " " + OrderType + " MN " + OrderMagicNumber + " OPEN " + OrderOpenPrice + " " + OrderOpenTime + " VOL " + OrderLots + " SL " + OrderStopLoss + " TP " + OrderTakeProfit + " COM " + OrderCommission + " SWAP " + OrderSwap + " PROF " + OrderProfit + " EXP " + OrderExpiration + " CLOSE " + OrderClosePrice + " " + OrderCloseTime + " COMMENT " + OrderComment + "\n";
            ret2 = ret1 = true;
          }
          else
          {
            norob += OrderSymbol + ": " + OrderTicket + " " + OrderType + " MN " + OrderMagicNumber + " OPEN " + OrderOpenPrice + " " + OrderOpenTime + " VOL " + OrderLots + " SL " + OrderStopLoss + " TP " + OrderTakeProfit + " COM " + OrderCommission + " SWAP " + OrderSwap + " PROF " + OrderProfit + " EXP " + OrderExpiration + " CLOSE " + OrderClosePrice + " " + OrderCloseTime + " COMMENT " + OrderComment + "\n";
            ret2 = true;
          }
        }
        else
        {
          int errno = mt4.GetLastError();
          string desc = Lib.ErrorDescription(errno);
          throw new Exception("rawPositionsListing(): OrderSelect(" + i + ", SELECT_BY_POS, MODE_TRADES) had error " + errno + " (" + desc + ")");
        }
      }
      str += rob;
      str += "ACTIVE - OTHER:\n" + norob;
      str += "HISTORY:\n";
      for (int i = mt4.OrdersHistoryTotal() - 1; i >= 0; i--)
      {
        if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_HISTORY))
        {
          int OrderTicket = mt4.OrderTicket();
          int OrderMagicNumber = mt4.OrderMagicNumber();
          double OrderClosePrice = mt4.OrderClosePrice();
          DateTime OrderCloseTime = mt4.OrderCloseTime();
          string OrderComment = mt4.OrderComment();
          double OrderCommission = mt4.OrderCommission();
          DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
          OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
          double OrderLots = mt4.OrderLots();
          double OrderOpenPrice = mt4.OrderOpenPrice();
          DateTime OrderOpenTime = mt4.OrderOpenTime();
          double OrderProfit = mt4.OrderProfit();
          double OrderStopLoss = mt4.OrderStopLoss();
          double OrderSwap = mt4.OrderSwap();
          string OrderSymbol = mt4.OrderSymbol();
          double OrderTakeProfit = mt4.OrderTakeProfit();
          TradeOperation OrderType = mt4.OrderType();
          str += OrderSymbol + ": " + OrderTicket + " " + OrderType + " MN " + OrderMagicNumber + " OPEN " + OrderOpenPrice + " " + OrderOpenTime + " VOL " + OrderLots + " SL " + OrderStopLoss + " TP " + OrderTakeProfit + " COM " + OrderCommission + " SWAP " + OrderSwap + " PROF " + OrderProfit + " EXP " + OrderExpiration + " CLOSE " + OrderClosePrice + " " + OrderCloseTime + " COMMENT " + OrderComment + "\n";
        }
        else
        {
          int errno = mt4.GetLastError();
          string desc = Lib.ErrorDescription(errno);
          throw new Exception("rawPositionsListing(): OrderSelect(" + i + ", SELECT_BY_POS, MODE_HISTORY) had error " + errno + " (" + desc + ")");
        }
      }
      Logger.Info("Raw position listing:\n" + str);
      return Tuple.Create(ret1, ret2);
    }

    /// <summary>
    /// Adoptuje pozycje z MT4. Zwraca ile pozycji zaadoptowano.
    /// </summary>
    /// <returns>Ile pozycji zaadoptowano.</returns>
    /// <exception cref="Exception">Coś poszło nie tak i musimy kończyć program.</exception>
    public int rawPositionsAdoption()
    {
      int ret = 0;
      mt4.GetLastError(); // wyczyść bufor błędu
      for (int i = mt4.OrdersTotal() - 1; i >= 0; i--)
      {
        if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_TRADES))
        {
          int OrderTicket = mt4.OrderTicket();
          int OrderMagicNumber = mt4.OrderMagicNumber();
          double OrderClosePrice = mt4.OrderClosePrice();
          DateTime OrderCloseTime = mt4.OrderCloseTime();
          string OrderComment = mt4.OrderComment();
          double OrderCommission = mt4.OrderCommission();
          DateTime OrderExpiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); // może trochę szybsze niż mt4.ToDate()
          OrderExpiration = OrderExpiration.AddSeconds(mt4.OrderExpiration());
          double OrderLots = mt4.OrderLots();
          double OrderOpenPrice = mt4.OrderOpenPrice();
          DateTime OrderOpenTime = mt4.OrderOpenTime();
          double OrderProfit = mt4.OrderProfit();
          double OrderStopLoss = mt4.OrderStopLoss();
          double OrderSwap = mt4.OrderSwap();
          string OrderSymbol = mt4.OrderSymbol();
          double OrderTakeProfit = mt4.OrderTakeProfit();
          TradeOperation OrderType = mt4.OrderType();
          if (Lib.isIntToMagicOk(OrderMagicNumber))
          {
            Enums.PositionTactics tactics = Enums.PositionTactics.Aggressive;
            if ((int)Enums.MagicNumbers.Peak == OrderMagicNumber)
            {
              tactics = Enums.PositionTactics.Peak;
            }
            else if ((int)Enums.MagicNumbers.Strict == OrderMagicNumber)
            {
              tactics = Enums.PositionTactics.Strict;
            }
            Position p = Position.adopt(tactics, OrderType, OrderTicket, OrderClosePrice, OrderCloseTime, OrderComment, OrderCommission, OrderExpiration, OrderLots, OrderOpenPrice, OrderOpenTime, OrderProfit, OrderStopLoss, OrderSwap, OrderSymbol, OrderTakeProfit);
            positions[OrderTicket] = p;
            switch (tactics)
            {
              case Enums.PositionTactics.Aggressive:
                pos_aggressive[OrderTicket] = p;
                break;
              case Enums.PositionTactics.Peak:
                pos_peak[OrderTicket] = p;
                break;
              case Enums.PositionTactics.Strict:
                pos_strict[OrderTicket] = p;
                break;
            }
            ret++;
          }
        }
        else
        {
          int errno = mt4.GetLastError();
          string desc = Lib.ErrorDescription(errno);
          throw new Exception("rawPositionsAdoption(): OrderSelect(" + i + ", SELECT_BY_POS, MODE_TRADES) had error " + errno + " (" + desc + ")");
        }
      }
      for (int i = mt4.OrdersHistoryTotal() - 1; i >= 0; i--)
      {
        if (mt4.OrderSelect(i, SelectionType.SELECT_BY_POS, SelectionPool.MODE_HISTORY))
        {
          int OrderTicket = mt4.OrderTicket();
          int OrderMagicNumber = mt4.OrderMagicNumber();
          if (Lib.isIntToMagicOk(OrderMagicNumber))
          {
            nonadopted_oldies.Add(OrderTicket);
            Logger.Info("Added " + OrderTicket + " to nonadopted_oldies");
          }
        }
        else
        {
          int errno = mt4.GetLastError();
          string desc = Lib.ErrorDescription(errno);
          throw new Exception("rawPositionsAdoption(): OrderSelect(" + i + ", SELECT_BY_POS, MODE_HISTORY) had error " + errno + " (" + desc + ")");
        }
      }
      accounting(thorough: true);
      return ret;
    }

    /// <summary>
    /// Opis bilansu i depozytu konta.
    /// </summary>
    /// <returns>Opis bilansu i depozytu konta.</returns>
    public string accountingInfo()
    {
      return "balance: " + E.AccountBalance + " equity: " + E.AccountEquity + " free margin: " + E.AccountFreeMargin + " margin: " + E.AccountMargin + " profit: " + E.AccountProfit + " tick_val: " + E.TICKVALUE + "\nmargin required: " + E.MARGINREQUIRED + " init: " + E.MARGININIT + " hedged: " + E.MARGINHEDGED + " maintenance: " + E.MARGINMAINTENANCE;
    }

    /// <summary>
    /// Kod do testowania.
    /// </summary>
    public void test()
    {
      const int test_no = 3;
#pragma warning disable 0162
      // wstępny test pozycji pending (wygaśnięcie)
      if (test_no == 1)
      {
        // złóż zlecenie pending (nie do zrealizowania) 3.09.2007 o 0:50, zamknij o 4:10 (ale wcześniej wygasa)
        if (E.tick_counter == 7000) Logger.Info("TEST 1: A\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 7900)
        {
          openStrict_vol(Enums.LimitedDirection.Up, 1.3620M, 0.1M, 1);
          Logger.Info("TEST 1: A2\n" + positionsXML(include_historical: true));
          Console.WriteLine("KURWA1: " + testPositionsListing());
        }
        if (E.tick_counter == 7901) Logger.Info("TEST 1: B\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 15400)
        {
          while (pos_strict.Count() > 0)
          {
            foreach (var x in pos_strict.Keys)
            {
              deletePending(x);
              break;
            }
          }
          Console.WriteLine("KURWA2: " + testPositionsListing());
        }
        if (E.tick_counter == 15401) Logger.Info("TEST 1: C\n" + positionsXML(include_historical: true));
      }

      // wstępny test pozycji pending (skasowanie)
      if (test_no == 2)
      {
        // złóż zlecenie pending (nie do zrealizowania) 3.09.2007 o 0:50, zamknij o 4:10
        if (E.tick_counter == 7000) Logger.Info("TEST 2: A\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 7900)
        {
          openStrict_vol(Enums.LimitedDirection.Up, 1.3620M, 0.1M, 1);
          Logger.Info("TEST 2: A2\n" + positionsXML(include_historical: true));
          Console.WriteLine("KURWA1: " + testPositionsListing());
        }
        if (E.tick_counter == 7901) Logger.Info("TEST 2: B\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 8700)
        {
          while (pos_strict.Count() > 0)
          {
            foreach (var x in pos_strict.Keys)
            {
              deletePending(x);
              break;
            }
          }
          Console.WriteLine("KURWA2: " + testPositionsListing());
        }
        if (E.tick_counter == 8701) Logger.Info("TEST 2: C\n" + positionsXML(include_historical: true));
      }

      // testy pozycji aggressive
      if (test_no == 3)
      {
        // otwórz długą 3.09.2007 o 0:50, zamknij o 4:10
        if (E.tick_counter == 7000) Logger.Info("TEST 3: A\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 7900)
        {
          openAggressive_vol(Enums.LimitedDirection.Up, 0.1M, 1.3620M);
          Logger.Info("TEST 3: A2\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 7901) Logger.Info("TEST 3: B\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 15400)
        {
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 15401) Logger.Info("TEST 3: C\n" + positionsXML(include_historical: true));
      }

      // testy wszystkich rodzajów (zamknięcie na SL)
      if (test_no == 4)
      {
        // otwórz 3 na raz o 3.09.2007 o 0:30 i niech zatrzymają się na SL
        if (E.tick_counter == 5500) Logger.Info("TEST 4: A\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 5600)
        {
          Console.WriteLine("KURWA " + E.ASK + " " + E.BID + " " + E.STOPLEVEL + " " + E.FREEZELEVEL + " " + Position.getOP_min() + " " + Position.getOP_lim(TradeOperation.OP_BUYLIMIT) + " " + Position.getSL_lim(TradeOperation.OP_BUY) + " " + Position.getSL_lim2(TradeOperation.OP_BUY) + " " + Position.getTP_lim(TradeOperation.OP_BUY) + " " + Position.getTP_lim2(TradeOperation.OP_BUY));
          Console.WriteLine("KURWA " + Position.getOP_lim(TradeOperation.OP_BUY) + " " + Position.getOP_lim(TradeOperation.OP_SELL) + " " + Position.getSL_lim(TradeOperation.OP_SELL) + " " + Position.getSL_lim2(TradeOperation.OP_SELL) + " " + Position.getTP_lim(TradeOperation.OP_SELL) + " " + Position.getTP_lim2(TradeOperation.OP_SELL));
          openAggressive_vol(Enums.LimitedDirection.Up, 0.1M, 1.3626M);
          openPeak_vol(Enums.LimitedDirection.Up, 0.1M, 1.3626M);
          openStrict_vol(Enums.LimitedDirection.Up, 1.3629M, 0.1M, 1.3626M);
          Logger.Info("TEST 4: A2\n" + positionsXML(include_historical: true));
          mt4.setEmergencyMode();
        }
        if (E.tick_counter == 5601)
        {
          openAggressive_vol(Enums.LimitedDirection.Up, 0.1M, 1.3626M);
        }
        if (E.tick_counter == 7800) Logger.Info("TEST 4: B\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 10000)
        {
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 10001) Logger.Info("TEST 4: C\n" + positionsXML(include_historical: true));
      }

      // testy wszystkich rodzajów (zamknięcie na TP)
      if (test_no == 5)
      {
        // otwórz 3 na raz o 3.09.2007 o 0:30 i niech zatrzymają się na TP
        if (E.tick_counter == 5500) Logger.Info("TEST 5: A\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 5600)
        {
          openAggressive_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M, 1.3636M);
          openPeak_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M, 1.3636M);
          openStrict_vol(Enums.LimitedDirection.Up, 1.3629M, 0.1M, 1.36M, 1.3636M);
          Logger.Info("TEST 5: A2\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 7800) Logger.Info("TEST 5: B\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 11560) Logger.Info("TEST 5: B2\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 11562)
        {
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 11563) Logger.Info("TEST 5: C\n" + positionsXML(include_historical: true));
      }

      // testy wszystkich rodzajów (dwa zamknięcia częściowe, jedno całkowite)
      if (test_no == 6)
      {
        // otwórz 3 na raz o 3.09.2007 o 0:30
        if (E.tick_counter == 5500) Logger.Info("TEST 6: A\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 5600)
        {
          openAggressive_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M);
          openPeak_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M);
          openStrict_vol(Enums.LimitedDirection.Up, 1.3629M, 0.1M, 1.36M);
          Logger.Info("TEST 6: A2\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 7777)
        {
          closeOpened(1, 0, 0.02M);
          Logger.Info("TEST 6: B1\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 7800) Logger.Info("TEST 6: B2\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 9575)
        {
          closeOpened(2, 0, 0.06M);
          closeOpened(3);
          Logger.Info("TEST 6: B3\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 11560) Logger.Info("TEST 6: B4\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 11562)
        {
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_peak.Count() > 0)
          {
            foreach (var x in pos_peak.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_strict.Count() > 0)
          {
            foreach (var x in pos_strict.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 11563) Logger.Info("TEST 6: C\n" + positionsXML(include_historical: true));
      }

      // testy wszystkich rodzajów (trzy zamknięcia wzajemne w tym dwa częściowe)
      if (test_no == 7)
      {
        // otwórz 5 na raz o 3.09.2007 o 0:30
        if (E.tick_counter == 5500) Logger.Info("TEST 7: A\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 5600)
        {
          openAggressive_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M);
          openAggressive_vol(Enums.LimitedDirection.Down, 0.1M, 1.5M);
          openPeak_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M);
          openPeak_vol(Enums.LimitedDirection.Down, 0.2M, 1.37M);
          openStrict_vol(Enums.LimitedDirection.Up, 1.3629M, 0.2M, 1.36M);
          Logger.Info("TEST 7: A2\n" + positionsXML(include_historical: true));
        }
        //7783
        if (E.tick_counter == 7905)
        {
          Logger.Info("KURWA " + E.BID + " " + E.ASK + " " + E.SPREAD + " " + E.spread);
          closeOpenedWith(1, 2);
          Logger.Info("TEST 7: B1_1\n" + positionsXML(include_historical: true));
          closeOpenedWith(3, 4);
          Logger.Info("TEST 7: B1_2\n" + positionsXML(include_historical: true));
        }
        //7800
        if (E.tick_counter == 7910) Logger.Info("TEST 7: B2\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 9575)
        {
          closeOpenedWith(5, 6);
          Logger.Info("TEST 7: B3\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 11560) Logger.Info("TEST 7: B4\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 11562)
        {
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_peak.Count() > 0)
          {
            foreach (var x in pos_peak.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_strict.Count() > 0)
          {
            foreach (var x in pos_strict.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 11563) Logger.Info("TEST 7: C\n" + positionsXML(include_historical: true));
      }

      // testy zmian parametrów zlecenia otwartego i oczekującego
      if (test_no == 8)
      {
        // otwórz 5 na raz o 3.09.2007 o 0:30
        if (E.tick_counter == 5500) Logger.Info("TEST 8: A\n" + positionsXML(include_historical: true));
        if (E.tick_counter == 5600)
        {
          openAggressive_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M);
          openStrict_vol(Enums.LimitedDirection.Down, 1.5M, 0.2M, 1.6M);
          Logger.Info("TEST 8: A2\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 6000)
        {
          Logger.Info("KURWA 0: " + E.BID + " " + E.ASK + " " + E.STOPLEVEL + " " + E.TIME);
          Logger.Info("KURWA 1: " + positions[1].getSL_lim() + " " + positions[1].getSL_lim2() + " " + positions[1].getTP_lim() + " " + positions[1].getTP_lim2());
          Logger.Info("KURWA 2: " + positions[2].getSL_lim() + " " + positions[2].getSL_lim2() + " " + positions[2].getTP_lim() + " " + positions[2].getTP_lim2() + " " + positions[2].getOP_lim() + " " + Position.getOP_min());
          modifyOpened(1, 1.361M, 10);
          modifyPending(2, 1.5M, 0.1M, 1.45M, 20);
          Logger.Info("TEST 8: B1\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 12670)
        {
          modifyOpened(1, 1.3633M);
          Logger.Info("TEST 8: B2\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 13000)
        {
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_peak.Count() > 0)
          {
            foreach (var x in pos_peak.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_strict.Count() > 0)
          {
            foreach (var x in pos_strict.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 13001) Logger.Info("TEST 8: C\n" + positionsXML(include_historical: true));
      }

      // testy wyciśnięć
      if (test_no == 9)
      {
        // otwórz 3 na raz o 3.09.2007 o 0:30
        if (E.tick_counter == 5500) Logger.Info("TEST 9: A\n" + positionsXML(include_historical: true, evolution: true));
        if (E.tick_counter == 5600)
        {
          openAggressive_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M);
          openPeak_vol(Enums.LimitedDirection.Up, 0.1M, 1.36M);
          openStrict_vol(Enums.LimitedDirection.Up, 1.3629M, 0.1M, 1.36M);
          Logger.Info("TEST 9: A2\n" + positionsXML(include_historical: true, evolution: true));
        }
        if (E.tick_counter == 7915)
        {
          // 3IX, 0:55
          positions[1].setToSqueeze();
          positions[3].setToSqueeze();
          squeeze(1);
          squeeze(3);
          Logger.Info("TEST 9: B1\n" + positionsXML(include_historical: true, evolution: true));
        }
        if (E.tick_counter == 10000) Logger.Info("TEST 9: B2\n" + positionsXML(include_historical: true, evolution: true));
        if (E.tick_counter == 12410)
        {
          // 3IX, 2:00
          positions[2].setToSqueeze();
          squeeze(2);
          Logger.Info("TEST 9: B3\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 15000) Logger.Info("TEST 9: B4\n" + positionsXML(include_historical: true, evolution: true));
        if (E.tick_counter == 15001)
        {
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_peak.Count() > 0)
          {
            foreach (var x in pos_peak.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_strict.Count() > 0)
          {
            foreach (var x in pos_strict.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 15002) Logger.Info("TEST 9: C\n" + positionsXML(include_historical: true, evolution: true));
      }

      // testy wyliczania straty depozytu
      if (test_no == 10)
      {
        // otwórz długą 3.09.2007 o 0:40, zamknij o 4:10
        if (E.tick_counter == 6650)
        {
          openAggressive(Enums.LimitedDirection.Up, 1.3620M);
          Logger.Info("ACCINFO 1\n" + accountingInfo());
        }
        if (E.tick_counter == 6651)
        {
          Logger.Info("TEST 10: A1\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 7900)
        {
          Logger.Info("TEST 10: A2\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 8779)
        {
          modifyOpened(1, 1.3625M);
          Logger.Info("TEST 10: A3\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 14123)
        {
          modifyOpened(1, 1.3630M);
          Logger.Info("TEST 10: A4\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 15399)
        {
          modifyOpened(1, 1.3634M);
          openPeak(Enums.LimitedDirection.Down, 1.4M);
          Logger.Info("TEST 10: A5\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 15400)
        {
          Logger.Info("TEST 10: A6\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_peak.Count() > 0)
          {
            foreach (var x in pos_peak.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 15401) Logger.Info("TEST 10: B\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
      }

      // testy wyliczania straty kapitału
      if (test_no == 11)
      {
        // otwórz krótką 3.09.2007 o 0:10, zamknij na SL
        if (E.tick_counter == 2315)
        {
          Logger.Info("ACCINFO 1\n" + accountingInfo());
          openAggressive(Enums.LimitedDirection.Down, 1.3635M);
          Logger.Info("TEST 11: A1\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 2316)
        {
          Logger.Info("TEST 11: A2\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 7979)
        {
          Logger.Info("TEST 11: A3\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 7980)
        {
          Logger.Info("TEST 11: A4\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
        }
        if (E.tick_counter == 12670)
        {
          Logger.Info("TEST 11: A5\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
          while (pos_aggressive.Count() > 0)
          {
            foreach (var x in pos_aggressive.Keys)
            {
              closeOpened(x);
              break;
            }
          }
          while (pos_peak.Count() > 0)
          {
            foreach (var x in pos_peak.Keys)
            {
              closeOpened(x);
              break;
            }
          }
        }
        if (E.tick_counter == 12671) Logger.Info("TEST 11: B\n" + accountingInfo() + "\n" + positionsXML(include_historical: true));
      }
    }
#pragma warning restore 0162
  }
}
