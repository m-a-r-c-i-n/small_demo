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
  ///  Klasa opisująca pozycję.
  /// </summary>
  public class Position
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
    /// Wykres w średniej perspektywie czasowej.
    /// </summary>
    private static Chart middleChart;
    /// <summary>
    /// Obiekt używany do mierzenia czasu wykonania funkcji MT4 operujących na zleceniach.
    /// </summary>
    private static System.Diagnostics.Stopwatch _sw = new System.Diagnostics.Stopwatch();

    /// <summary>
    /// Snapshot z chwili złożenia zlecenia.
    /// </summary>
    public readonly Snapshot opening_snapshot;

    /// <summary>
    /// Rozpiętość wstęgi Bollingera z chwili złożenia zlecenia.
    /// </summary>
    public readonly decimal bol_span;

    /// <summary>
    /// Czas utworzenia (obiektu) zlecenia (UTC), komputer z robotem.
    /// </summary>
    public readonly DateTime creation_time_local;
    /// <summary>
    /// Czas utworzenia zlecenia (czas ostatniego ticku), serwer brokera.
    /// </summary>
    public readonly DateTime creation_time;
    /// <summary>
    /// Numer ticka, po którym utworzono zlecenie.
    /// </summary>
    public readonly long creation_tick;
    /// <summary>
    /// Utworzony przez MT4 numer zlecenia. -1 wtw. gdy nie wiadomo czy ostatecznie zlecenie weszło.
    /// </summary>
    public int id { get; private set; }
    /// <summary>
    /// Magiczna liczba (nieunikatowa etykietka).
    /// </summary>
    public readonly Enums.MagicNumbers magic_num;

    /// <summary>
    /// Symbol, jakiego dotyczy zlecenie.
    /// </summary>
    public readonly string symbol;
    /// <summary>
    /// Kierunek żądanej pozycji.
    /// </summary>
    public readonly Enums.LimitedDirection dir;
    /// <summary>
    /// Zlecona operacja (do otwarcia pozycji).
    /// </summary>
    public readonly TradeOperation operation;
    /// <summary>
    /// Aktualna operacja (początkowo równa operation, może się zmienić gdy zlecenie oczekujące zostanie otwarte).
    /// </summary>
    public TradeOperation current_operation { get; private set; }
    /// <summary>
    /// Rozmiar pozycji (może się zmniejszyć przy częściowym zamknięciu).
    /// </summary>
    public double volume { get; private set; }
    /// <summary>
    /// Żądana cena otwarcia zlecenia.
    /// </summary>
    public double wanted_opening_price { get; private set; }
    /// <summary>
    /// Dozwolony poślizg przy otwarciu (w punktach).
    /// </summary>
    public readonly int allowed_opening_slippage;
    /// <summary>
    /// Poziom stop loss przy otwarciu zlecenia.
    /// </summary>
    public double wanted_opening_SL { get; private set; }
    /// <summary>
    /// Poziom take profit przy otwarciu zlecenia.
    /// </summary>
    public double wanted_opening_TP { get; private set; }
    /// <summary>
    /// Data wygaśnięcia zlecenia gdy pozycja nie została od razu otwarta.
    /// </summary>
    public DateTime opening_expiration { get; private set; }
    /// <summary>
    /// Komentarz początkowy do zlecenia.
    /// </summary>
    public string opening_comment { get; private set; }
    /// <summary>
    /// Z jakiej taktyki otwarcia i zarządzania pozycją korzystamy.
    /// </summary>
    public readonly Enums.PositionTactics tactics;
    /// <summary>
    /// Lista zmian parametrów pozycji/zlecenia (nie uwzględniamy tu zmian wolumenu wynikających z zamykania pozycji - one naprawdę otwierają nowe zlecenie z nowym id). Każdy element to (tick-po-którym-dokonujemy-zmiany, czas zmiany, nowy poziom S/L, nowy poziom T/P).
    /// NB: Można modyfikować jeszcze nową cena otwarcia i czas wygaśnięcia ale one dotyczą tylko zleceń oczekujących i nie logujemy w ogóle zmian parametrów dla Pending.
    /// </summary>
    public readonly List<Tuple<long, DateTime, double, double>> params_changes;
    /// <summary>
    /// Rzeczywista cena otwarcia pozycji.
    /// </summary>
    public double opening_price { get; private set; }
    /// <summary>
    /// Rzeczywisty czas otwarcia pozycji (przy zmianie typu pozycji z pending na opened przestaje być równy creation_time, staje się czasem otwarcia pozycji). Także przy poslizgu czasowym serwera może się różnić.
    /// </summary>
    public DateTime opening_time { get; private set; }
    /// <summary>
    /// Aktualny rzeczywisty poziom stop loss.
    /// </summary>
    public double SL { get; private set; }
    /// <summary>
    /// Aktualny rzeczywisty poziom take profit.
    /// </summary>
    public double TP { get; private set; }
    /// <summary>
    /// Dozwolony poślizg przy zamknięciu (w punktach).
    /// </summary>
    public int allowed_closing_slippage { get; private set; }
    /// <summary>
    /// Żądana cena zamknięcia zlecenia.
    /// </summary>
    public double wanted_closing_price { get; private set; }
    /// <summary>
    /// Rzeczywista cena zamknięcia pozycji.
    /// </summary>
    public double closing_price { get; private set; }
    /// <summary>
    /// Rzeczywisty czas zamknięcia pozycji.
    /// </summary>
    public DateTime closing_time { get; private set; }
    /// <summary>
    /// ID pozycji (w tym samym kierunku, wcześniejszej), z której ta powstała przez częściowe zamknięcie. Lub -1 gdy nie ma takiej.
    /// </summary>
    public readonly int predecessor;
    /// <summary>
    /// ID pozycji (w tym samym kierunku, późniejszej), która powstała z tej przez częściowe zamknięcie. Lub -1 gdy nie ma takiej.
    /// </summary>
    public int successor { get; private set; }
    /// <summary>
    /// ID pozycji (w przeciwnym kierunku), której użyliśmy do zamknięcia tej. Lub -1 gdy nie ma takiej.
    /// </summary>
    public int closed_by { get; private set; }
    /// <summary>
    /// Prowizja za pozycję.
    /// </summary>
    public double commission { get; private set; }
    /// <summary>
    /// Swap do zapłacenia za pozycję.
    /// </summary>
    public double swap { get; private set; }
    /// <summary>
    /// Zysk na pozycji.
    /// </summary>
    public double profit { get; private set; }
    /// <summary>
    /// Całkowity zysk na pozycji (profit+swap+commission).
    /// </summary>
    public double total_profit { get; private set; }
    /// <summary>
    /// Jaki jest stan zlecenia: oczekujące, otwarta pozycja, zamknięta pozycja, nieokreślony, skasowane.
    /// </summary>
    public Enums.PositionStatus status { get; private set; }
    /// <summary>
    /// Jaki jest finansowy stan zlecenia (pod względem ceny obecnej i poziomu S/L): stratne, optymistyczny break-even, przesuwamy się w kierunku mocnego break-even, mocny (uwzględnia pesymistycznie niekorzystny ruch cen) break-even, za mocnym break-even (zarabiamy i zabezpieczyliśmy część zysku).
    /// </summary>
    public Enums.PositionProfitStatus profit_status { get; private set; }
    /// <summary>
    /// Jaki jest finansowy stan zlecenia (pod względem ceny obecnej i ceny otwarcia): stratne, optymistyczny break-even, przesuwamy się w kierunku mocnego break-even, mocny (uwzględnia pesymistycznie niekorzystny ruch cen) break-even, za mocnym break-even (zarabiamy).
    /// </summary>
    public Enums.PositionProfitStatus profit_status_volatile { get; private set; }
    /// <summary>
    /// Rzeczywisty poślizg podczas otwarcia pozycji. Gdy większa od 0 - zyskaliśmy (na naszą korzyść), gdy mniejsza od 0 - straciliśmy. Podany w E.POINT.
    /// </summary>
    public int opening_slippage { get; private set; }
    /// <summary>
    /// Rzeczywisty poślizg podczas zamknięcia pozycji. Gdy większa od 0 - zyskaliśmy (na naszą korzyść), gdy mniejsza od 0 - straciliśmy. Podany w E.POINT.
    /// </summary>
    public int closing_slippage { get; private set; }
    /// <summary>
    /// ID zawarte w komentarzu.
    /// </summary>
    public string id_from_comment { get; private set; }

    /// <summary>
    /// Jaka zmiana ceny (wartość dodana/odjęta od ceny otwarcia pozycji) równoważy aktualny swap. Ujemna wtw. gdy zmiana niekorzystna dla nas.
    /// </summary>
    public double price_for_swap { get; private set; }
    /// <summary>
    /// Jaka zmiana ceny (wartość dodana/odjęta od ceny otwarcia pozycji) równoważy aktualną prowizję. Ujemna wtw. gdy zmiana niekorzystna dla nas.
    /// </summary>
    public double price_for_commission { get; private set; }
    /// <summary>
    /// Jaka zmiana ceny (wartość dodana/odjęta od ceny otwarcia pozycji) równoważy aktualną prowizję i swap. Ujemna wtw. gdy zmiana niekorzystna dla nas.
    /// </summary>
    public double price_for_costs { get; private set; }
    /// <summary>
    /// Jaka zmiana ceny (wartość dodana/odjęta od ceny otwarcia pozycji) równoważy aktualną prowizję i swap. Ujemna wtw. gdy zmiana niekorzystna dla nas. Wartość kwantowana punktem w niekorzystną dla nas stronę (w dół).
    /// </summary>
    public double price_for_costs_normalized { get; private set; }
    /// <summary>
    /// Iloraz ruchu cen od ceny otwarcia do obecnej przez profit (wartość bezwzględna).
    /// </summary>
    private decimal price_per_profit_unit;

    /// <summary>
    /// Cena otwarcia pogorszona o koszty prowizji i swapu.
    /// </summary>
    public double break_even { get; private set; }
    /// <summary>
    /// Czy pozycja była chociaż raz weryfikowana danymi z MT4.
    /// </summary>
    public bool ever_updated { get; private set; }

    /// <summary>
    /// Czy staramy się wycisnąć (okienko SL-TP tak wąskie jak się da, przesuwamy tylko w dobrą stronę) pozycję. Ustawione także gdy beingTightlySqeezed==true.
    /// </summary>
    public bool beingSqeezed { get; private set; }
    /// <summary>
    /// Czy staramy się "w miejscu" wycisnąć (okienko SL-TP tak wąskie jak się da, tylko zawężamy) pozycję.
    /// </summary>
    public bool beingTightlySqeezed { get; private set; }
    /// <summary>
    /// Kiedy (czas ticka) rozpoczęliśmy wyciskanie - żeby ocenić czy jest sens się tak dalej bawić.
    /// </summary>
    public DateTime beingSqeezed_date { get; private set; }
    /// <summary>
    /// Kiedy (czas ticka) rozpoczęliśmy wyciskanie "w miejscu" - żeby ocenić czy jest sens się tak dalej bawić.
    /// </summary>
    public DateTime beingTightlySqeezed_date { get; private set; }
    /// <summary>
    /// Kiedy (tick) rozpoczęliśmy wyciskanie - żeby ocenić czy jest sens się tak dalej bawić.
    /// </summary>
    public long beingSqeezed_tick { get; private set; }
    /// <summary>
    /// Kiedy (tick) rozpoczęliśmy wyciskanie "w miejscu" - żeby ocenić czy jest sens się tak dalej bawić.
    /// </summary>
    public long beingTightlySqeezed_tick { get; private set; }
    /// <summary>
    /// Przy jakiej cenie rozpoczęliśmy wyciskanie - żeby ocenić czy jest sens się tak dalej bawić.
    /// </summary>
    public double beingSqeezed_price { get; private set; }
    /// <summary>
    /// Przy jakiej cenie rozpoczęliśmy wyciskanie "w miejscu" - żeby ocenić czy jest sens się tak dalej bawić.
    /// </summary>
    public double beingTightlySqeezed_price { get; private set; }

    /// <summary>
    /// Ile potencjalnie może wynieść maksymalna strata na pozycji (zamknięcie na S/L z poślizgiem) w walucie depozytowej rachunku. Liczona od ceny otwarcia. Gdy S/L już zabezpieczył transakcję, to 0.
    /// </summary>
    public decimal maxLossFromOpening { get; private set; }
    /// <summary>
    /// Ile potencjalnie może wynieść maksymalna strata na pozycji (zamknięcie na S/L z poślizgiem) w walucie depozytowej rachunku. Liczona od ceny aktualnej. Gdy S/L już zabezpieczył transakcję, to 0.
    /// </summary>
    public decimal maxLossFromCurrent { get; private set; }

    /// <summary>
    /// Maksymalna cena (potencjalnego zamknięcia) od momentu otwarcia transakcji.
    /// </summary>
    public decimal height { get; private set; }
    /// <summary>
    /// Czy właśnie została ustawiona nowa cena height (uaktualniane przy każdym ticku).
    /// </summary>
    public bool new_height { get; private set; }
    /// <summary>
    /// Minimalna cena (potencjalnego zamknięcia) od momentu otwarcia transakcji.
    /// </summary>
    public decimal low { get; private set; }
    /// <summary>
    /// Czy właśnie została ustawiona nowa cena low (uaktualniane przy każdym ticku).
    /// </summary>
    public bool new_low { get; private set; }

    /// <summary>
    /// Ile pipsów uzyskaliśmy na transakcji (przed zamknięciem: 0).
    /// </summary>
    public int pips { get; private set; }
    /// <summary>
    /// Ile pipsów mogliśmy uzyskać na transakcji gdyby zamknąć w ekstremum (przed zamknięciem: 0).
    /// </summary>
    public int pips_pot { get; private set; }

    /// <summary>
    /// Przybliżone koszty wynikające ze spreadu - dodawane przy otwarciu i przy zamknięciu pozycji. Gdy ujemne - tracimy, gdy dodatnie (chyba nigdy ;) ) - zyskujemy.
    /// </summary>
    public decimal spread_costs { get; private set; }
    /// <summary>
    /// Przybliżone koszty wynikające z poślizgu - dodawane przy otwarciu i przy zamknięciu pozycji. Gdy ujemne - tracimy, gdy dodatnie (chyba nigdy ;) ) - zyskujemy.
    /// </summary>
    public decimal slippage_costs { get; private set; }
    /// <summary>
    /// Przybliżona kwota jaką ryzykujemy otwierając pozycję.
    /// </summary>
    public decimal initialRisk { get; private set; }
    /// <summary>
    /// Kwota jaką jest warta pozycja na otwarciu (po podzieleniu przez dźwignię).
    /// </summary>
    public decimal money { get; private set; }

    /// <summary>
    /// Współczynnik przyspieszenia (do systemu parabolicznego).
    /// </summary>
    public decimal AF;
    /// <summary>
    /// Wartość hipotytecznego SL dużej dokładności w systemie parabolicznym.
    /// </summary>
    public decimal SL_para;

    /// <summary>
    /// Data (czas serwera) ostatniej modyfikacji pozycji.
    /// </summary>
    public DateTime last_modification_date { get; private set; }

    /// <summary>
    /// Ostatni tick, w którym uaktualnialiśmy (do statusu CLosed/Deleted) pozycję.
    /// </summary>
    public long last_updated_tick { get; private set; }

    /// <summary>
    /// Czy stosowane jest luźne wyciskanie (używane czasem w końcowej fazie prowadzenia pozycji).
    /// </summary>
    public bool loose_trailing_squeezing = false;
    /// <summary>
    /// Data ostatniej zmiany parametrów SL (i może TP) w luźnym wyciskaniu.
    /// </summary>
    public DateTime loose_trailing_squeezing_time;
    /// <summary>
    /// Ostatnia odległość SL-cena w luźnym wyciskaniu.
    /// </summary>
    public decimal loose_trailing_squeezing_sl_dist;
    /// <summary>
    /// Cena podczas ostatniego uaktualnienia odległości SL-cena w luźnym wyciskaniu.
    /// </summary>
    public decimal loose_trailing_squeezing_last_price;

    /// <summary>
    /// Maksymalny zły poślizg, jakiego się spodziewamy (dla pozycji zamkniętej - przy zamykaniu).
    /// </summary>
    public decimal bad_slippage { get; private set; }

    /// <summary>
    /// Ile słupków od złożenia zlecenia (pending) do otwarcia pozycji (1 - ten sam słupek).
    /// </summary>
    public int age_before_opening { get; private set; }
    /// <summary>
    /// Wartość maksymalna ceny na wykresie w słupkach od złożenia zlecenia do otwarcia zlecenia (co najmniej 1 słupek).
    /// </summary>
    public decimal max_in_age_before_opening { get; private set; }
    /// <summary>
    /// Wartość minimalna ceny na wykresie w słupkach od złożenia zlecenia do otwarcia zlecenia (co najmniej 1 słupek).
    /// </summary>
    public decimal min_in_age_before_opening { get; private set; }
    /// <summary>
    /// Ważny poziom SR w kierunku otwarcia pozycji strict.
    /// </summary>
    public decimal imp_SR_for_strict { get; private set; }

    /// <summary>
    /// Ekstremum w dobrym kierunku, pomocnicze dla wyznaczania SL pozycji Strict.
    /// </summary>
    public decimal extremum_plus_strict;
    /// <summary>
    /// Ekstremum w złym kierunku, pomocnicze dla wyznaczania SL pozycji Strict.
    /// </summary>
    public decimal extremum_minus_strict;

    /// <summary>
    /// Czy wykonano pewne sprawdzenie pomocnicze pomiędzy złożeniem zlecenia a otwarciem pozycji.
    /// </summary>
    public bool strict_filter_helper_checking1 = false;
    /// <summary>
    /// Czy wykonano pewne sprawdzenie pomocnicze pomiędzy złożeniem zlecenia a otwarciem pozycji.
    /// </summary>
    public bool strict_filter_helper_checking2 = false;
    /// <summary>
    /// Czy wykonano pewne sprawdzenie pomocnicze (trzy czarne kruki/trzech białych żołnierzy) pomiędzy złożeniem zlecenia a otwarciem pozycji.
    /// </summary>
    public bool strict_filter_helper_checking3 = false;
    /// <summary>
    /// Czy obecna jest formacja (np. trzy czarne kruki/trzech białych żołnierzy) pomiędzy złożeniem zlecenia a otwarciem pozycji. NB: formacja nie musi być ukończona przynajmniej słupek temu (w strefie odbijania SR często cena się lekko odbija i ostatnia cena zamknięcia ostatecznie nie przebija poprzedniej, czasem w ostatnim słupku pojawia się doji lub nawet świeca o malutkim korpusie przeciwnego koloru) - dopuszczamy ustawienie w jej ostatnim słupku, gdy akurat jest obecna.
    /// </summary>
    public bool strict_filter_helper_checking4 = false;
    /// <summary>
    /// Data potencjalnego ukończenia (ostatni słupek) formacji z strict_filter_helper_checking4. NB: formacja nie musi być ukończona przynajmniej słupek temu (w strefie odbijania SR często cena się lekko odbija i ostatnia cena zamknięcia ostatecznie nie przebija poprzedniej, czasem w ostatnim słupku pojawia się doji lub nawet świeca o malutkim korpusie przeciwnego koloru) - dopuszczamy ustawienie w jej ostatnim słupku, gdy akurat jest obecna.
    /// </summary>
    public DateTime strict_filter_helper_checking4_date;

    /// <summary>
    /// Wartość VI_Max w momencie złożenia zlecenia.
    /// </summary>
    public decimal initial_VIM { get; private set; }
    /// <summary>
    /// Wartość StdDev w momencie złożenia zlecenia.
    /// </summary>
    public decimal initial_StdDev { get; private set; }
    /// <summary>
    /// Wartość StdDev_MA w momencie złożenia zlecenia.
    /// </summary>
    public decimal initial_StdDev_MA { get; private set; }

    /// <summary>
    /// Niedawny (w stosunku do momentu złożenia zlecenia) ważny SR, żeby odróżnić krótką korektę (która może się odbić od tego SR) od zmiany sytuacji unieważniającej zlecenie oczekujące.
    /// </summary>
    public decimal strict_last_close_SR_helper;
    /// <summary>
    /// Czy ustawiono strict_last_close_SR_helper.
    /// </summary>
    public bool strict_last_close_SR_helper_isset;

    /// <summary>
    /// Data ostaniego zacieśnienia ochronnego SL. Ze względu na algorytm zacieśniania nie chcemy by działo się to kilka razy w słupku.
    /// </summary>
    public DateTime last_cutting_date { get; private set; }

    /// <summary>
    /// Uaktualnij last_cutting_date o ile faktycznie udało się ustawić SL w skutek zacieśniania.
    /// </summary>
    /// <param name="newSL">Żądany poziom SL.</param>
    public void updateLastCuttingDate(decimal newSL)
    {
      if (SL == (double)newSL)
      {
        // zacieśniliśmy SL z sukcesem
        last_cutting_date = E.TIME;
      }
    }

    /// <summary>
    /// Czy możemy zacieśnić ochronnie SL. Nie częściej niż raz na słupek.
    /// </summary>
    public bool canBeCut()
    {
      TimeSpan ts = E.TIME - last_cutting_date;
      return ts.TotalMinutes > middleChart.barTime();
    }

    /// <summary>
    /// Ustaw imp_SR_for_strict.
    /// </summary>
    /// <param name="_imp_SR_for_strict">Nowa wartość imp_SR_for_strict.</param>
    public void setImpSRForStrict(decimal _imp_SR_for_strict)
    {
      imp_SR_for_strict = _imp_SR_for_strict;
    }

    /// <summary>
    /// Inicjalizuj statyczne pola klasy.
    /// </summary>
    /// <param name="_mt4">Referencja do obiektu strategii.</param>
    /// <param name="_Logger">Referencja do obiektu loggera.</param>
    /// <param name="_middleChart">Referencja do obiektu wykresu w średniej perspektywie czasowej.</param>
    public static void initialize(nj4x.wymiatacz_fx.Strategy.Marcinek _mt4, log4net.ILog _Logger, Chart _middleChart)
    {
      mt4 = _mt4;
      Logger = _Logger;
      middleChart = _middleChart;
    }

    /// <summary>
    /// Ustaw pozycję jako wyciskaną.
    /// </summary>
    public void setToSqueeze()
    {
      if (status == Enums.PositionStatus.Opened)
      {
        if (!beingSqeezed)
        {
          beingSqeezed = true;
          if (dir == Enums.LimitedDirection.Up)
          {
            beingSqeezed_price = (double)E.BID;
          }
          else
          {
            beingSqeezed_price = (double)E.ASK;
          }
          beingSqeezed_date = E.TIME;
          beingSqeezed_tick = E.tick_counter;
        }
      }
    }

    /// <summary>
    /// Ustaw pozycję jako wyciskaną "w miejscu".
    /// </summary>
    public void setToSqueezeTightly()
    {
      if (status == Enums.PositionStatus.Opened)
      {
        if (!beingTightlySqeezed)
        {
          beingTightlySqeezed = true;
          if (dir == Enums.LimitedDirection.Up)
          {
            beingTightlySqeezed_price = (double)E.BID;
          }
          else
          {
            beingTightlySqeezed_price = (double)E.ASK;
          }
          beingTightlySqeezed_date = E.TIME;
          beingTightlySqeezed_tick = E.tick_counter;
          if (!beingSqeezed)
          {
            beingSqeezed = true;
            beingSqeezed_price = beingTightlySqeezed_price;
            beingSqeezed_date = beingTightlySqeezed_date;
            beingSqeezed_tick = E.tick_counter;
          }
        }
      }
    }

    /// <summary>
    /// Czy warto pozycję dalej wyciskać (nie trwa to zbyt długo i ruch jest sensownie duży).
    /// </summary>
    /// <returns>Czy warto pozycję dalej wyciskać.</returns>
    public bool worthSqueezingFurther()
    {
      bool ret = false;
      if (beingSqeezed && !beingTightlySqeezed)
      {
        TimeSpan ts = E.TIME.Subtract(beingSqeezed_date);
        if (ts <= PositionClosingConfig.LOOSE_SQUEEZING_MAX_TIME_TIMESPAN)
        {
          if (ts <= PositionClosingConfig.LOOSE_SQUEEZING_MIN_TIME_TIMESPAN)
          {
            ret = true;
          }
          else
          {
            if (dir == Enums.LimitedDirection.Up)
            {
              if (Lib.comp((E.BID - (decimal)beingSqeezed_price), PositionClosingConfig.MIN_SQEEZING_SPEED * (decimal)ts.TotalMilliseconds / 60000) >= 0) ret = true;
            }
            else
            {
              if (Lib.comp(((decimal)beingSqeezed_price - E.ASK), PositionClosingConfig.MIN_SQEEZING_SPEED * (decimal)ts.TotalMilliseconds / 60000) >= 0) ret = true;
            }
          }
        }
      }
      return ret;
    }

    /// <summary>
    /// Czy warto pozycję dalej ciasno wyciskać (nie trwa to zbyt długo).
    /// </summary>
    /// <returns>Czy warto pozycję dalej ciasno wyciskać.</returns>
    public bool worthTightlySqueezingFurther()
    {
      bool ret = false;
      if (beingTightlySqeezed)
      {
        TimeSpan ts = E.TIME.Subtract(beingTightlySqeezed_date);
        if (ts <= PositionClosingConfig.LOOSE_SQUEEZING_MIN_TIME_TIMESPAN)
        {
          ret = true;
        }
      }
      return ret;
    }

    /// <summary>
    /// Wyciskaj pozycję (popraw S/L, T/P). Zwraca czy złożono zlecenie.
    /// </summary>
    /// <returns>Zwraca czy złożono zlecenie.</returns>
    public bool squeeze()
    {
      bool ret = false;
      if (beingSqeezed)
      {
        double _sl1 = (double)(getSL_lim());
        double _sl2 = (double)(getSL_lim2());
        double _tp1 = (double)(getTP_lim());
        double _tp2 = (double)(getTP_lim2());
        bool can_modify = false;
        bool do_modify = false;
        double p;
        if (dir == Enums.LimitedDirection.Up)
        {
          can_modify = Lib.comp(_sl1, _sl2) == 1;
          p = (double)E.BID;
          if (beingTightlySqeezed)
          {
            do_modify = (Lib.comp(_sl1, SL) == 0 && Lib.comp(_tp1, TP) == -1) || (Lib.comp(_sl1, SL) == 1 && Lib.comp(_tp1, TP) <= 0);
          }
          else
          {
            do_modify = (Lib.comp(_sl1, SL) == 0 && Lib.comp(_tp1, TP) == -1) || (Lib.comp(_sl1, SL) == 1);
          }
        }
        else
        {
          can_modify = Lib.comp(_tp1, _tp2) == 1;
          p = (double)E.ASK;
          if (beingTightlySqeezed)
          {
            do_modify = (Lib.comp(_sl1, SL) == 0 && Lib.comp(_tp1, TP) == 1) || (Lib.comp(_sl1, SL) == -1 && Lib.comp(_tp1, TP) >= 0);
          }
          else
          {
            do_modify = (Lib.comp(_sl1, SL) == 0 && Lib.comp(_tp1, TP) == 1) || (Lib.comp(_sl1, SL) == -1);
          }
        }
        if (can_modify)
        {
          if (do_modify)
          {
            // jest sens coś robić
            ret = true;
            if (!modify((decimal)_sl1, (decimal)_tp1)) Logger.Warn("squeeze(): problem in modifying " + id + ": " + p + " " + _sl1 + " " + _tp1);
          }
        }
        else
        {
          Logger.Warn("squeeze(): can't modify " + id + ": " + p + " " + _sl1 + " " + _sl2 + " " + _tp1 + " " + _tp1);
        }
      }
      return ret;
    }

    /// <summary>
    /// Stwórz obiekt pozycji.
    /// </summary>
    /// <param name="_id">Identyfikator (ticket). Może być -1 gdy początkowo nieznany (w skutek timeoutu) - wtedy update() uaktualni.</param>
    /// <param name="_creation_time">Czas utworzenia zlecenia (czas ostatniego ticku), serwer brokera.</param>
    /// <param name="_creation_tick">Numer ticka, po którym utworzono zlecenie.</param>
    /// <param name="_dir">Kierunek żądanej pozycji.</param>
    /// <param name="_operation">Zlecona operacja (do otwarcia pozycji).</param>
    /// <param name="_volume">Wolumen.</param>
    /// <param name="_wanted_opening_price">Żądana cena otwarcia zlecenia.</param>
    /// <param name="_allowed_opening_slippage">Dozwolony poślizg przy otwarciu (w punktach).</param>
    /// <param name="_wanted_opening_SL">Poziom stop loss przy otwarciu zlecenia.</param>
    /// <param name="_wanted_opening_TP">Poziom take profit przy otwarciu zlecenia.</param>
    /// <param name="_opening_expiration">Data wygaśnięcia zlecenia gdy pozycja nie została od razu otwarta.</param>
    /// <param name="_tactics">Z jakiej taktyki otwarcia i zarządzania pozycją korzystamy.</param>
    /// <param name="_opening_price">Rzeczywista cena otwarcia pozycji.</param>
    /// <param name="_opening_time">Rzeczywisty czas otwarcia pozycji (przy zmianie typu pozycji z pending na opened przestaje być równy creation_time, staje się czasem otwarcia pozycji).</param>
    /// <param name="_SL">Aktualny rzeczywisty poziom stop loss.</param>
    /// <param name="_TP">Aktualny rzeczywisty poziom take profit.</param>
    /// <param name="_predecessor">ID pozycji (w tym samym kierunku, wcześniejszej), z której ta powstała przez częściowe zamknięcie. Lub -1 gdy nie ma takiej.</param>
    /// <param name="_closing_price">Rzeczywista cena zamknięcia pozycji.</param>
    /// <param name="_closing_time">Rzeczywisty czas zamknięcia pozycji.</param>
    /// <param name="_commission">Prowizja za pozycję.</param>
    /// <param name="_swap">Swap do zapłacenia za pozycję.</param>
    /// <param name="_profit">Zysk na pozycji.</param>
    /// <param name="_status">Jaki jest stan zlecenia: oczekujące, otwarta pozycja, zamknięta pozycja, nieokreślony, skasowane.</param>
    /// <param name="_profit_status">Jaki jest finansowy stan zlecenia (pod względem ceny obecnej i zabezpieczenia się S/L).</param>
    /// <param name="_profit_status_volatile">Jaki jest finansowy stan zlecenia (pod względem ceny obecnej i ceny otwarcia).</param>
    /// <param name="_magic_num">Magiczna liczba (nieunikatowa etykietka).</param>
    /// <param name="_opening_comment"></param>
    private Position(int _id, DateTime _creation_time, long _creation_tick, Enums.LimitedDirection _dir, TradeOperation _operation, double _volume, double _wanted_opening_price, int _allowed_opening_slippage, double _wanted_opening_SL, double _wanted_opening_TP, DateTime _opening_expiration, Enums.PositionTactics _tactics, double _opening_price, DateTime _opening_time, double _SL, double _TP, int _predecessor, double _closing_price, DateTime _closing_time, double _commission, double _swap, double _profit, Enums.PositionStatus _status, Enums.PositionProfitStatus _profit_status, Enums.PositionProfitStatus _profit_status_volatile, Enums.MagicNumbers _magic_num, string _opening_comment = "", bool _ever_updated = false)
    {
      pips = pips_pot = 0;
      AF = 0;
      last_updated_tick = E.tick_counter;
      beingSqeezed = beingTightlySqeezed = false;
      beingSqeezed_price = beingTightlySqeezed_price = -1;
      last_cutting_date = beingSqeezed_date = beingTightlySqeezed_date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      ever_updated = _ever_updated;
      creation_time_local = DateTime.UtcNow;
      last_modification_date = creation_time = _creation_time;
      creation_tick = _creation_tick;
      id = _id;
      symbol = InitConfig.Allowed_Symbol;
      dir = _dir;
      current_operation = operation = _operation;
      volume = _volume;
      wanted_opening_price = _wanted_opening_price;
      allowed_opening_slippage = _allowed_opening_slippage;
      wanted_opening_SL = _wanted_opening_SL;
      wanted_opening_TP = _wanted_opening_TP;
      opening_expiration = _opening_expiration;
      opening_comment = _opening_comment;
      tactics = _tactics;
      params_changes = new List<Tuple<long, DateTime, double, double>>();
      allowed_closing_slippage = 0;
      wanted_closing_price = 0;
      successor = -1;
      closed_by = -1;
      opening_price = _opening_price;
      opening_time = _opening_time;
      SL = _SL;
      TP = _TP;
      predecessor = _predecessor;
      closing_price = _closing_price;
      closing_time = _closing_time;
      commission = _commission;
      swap = _swap;
      profit = _profit;
      total_profit = profit + swap + commission;
      status = _status;
      profit_status = _profit_status;
      profit_status_volatile = _profit_status_volatile;
      magic_num = _magic_num;
      opening_slippage = closing_slippage = 0; //ustawiane w update() i createSuccessor()
      id_from_comment = Position.idFromComment(opening_comment);
      price_per_profit_unit = 0;
      price_for_costs = price_for_swap = price_for_commission = 0;
      if (!Lib.isZero(profit))
      {
        decimal p = (decimal)opening_price;
        if (dir == Enums.LimitedDirection.Up)
        {
          p -= E.BID;
        }
        else
        {
          p -= E.ASK;
        }
        price_per_profit_unit = p / (decimal)profit;
        if (price_per_profit_unit < 0) price_per_profit_unit = -price_per_profit_unit;
      }
      if (!Lib.isZero(price_per_profit_unit))
      {
        // co prawda jeśli profit jest zerowy to nie mamy dokładnej wartości tylko przybliżoną na podstawie ostatnio znanej informacji, ale za to waluta depozytowa to nie musi być bazowa ani kwotowana i nie musimy uwzględniać dodatkowego kursu
        price_for_commission = (double)((decimal)commission * price_per_profit_unit);
        price_for_swap = (double)((decimal)swap * price_per_profit_unit);
        decimal pom = ((decimal)swap + (decimal)commission) * price_per_profit_unit;
        price_for_costs = (double)pom; // uwzględniamy cud: teoretycznie swap czasem może być dodatni
        price_for_costs_normalized = (double)((decimal)Math.Floor((double)((decimal)price_for_costs / E.POINT)) * E.POINT);
      }
      if (dir == Enums.LimitedDirection.Up)
      {
        break_even = opening_price - price_for_costs_normalized;
      }
      else
      {
        break_even = opening_price + price_for_costs_normalized;
      }

      maxLossFromOpening = calcPossibleLoss(from_current_price: false);
      maxLossFromCurrent = calcPossibleLoss(from_current_price: true);
      spread_costs = -E.spread / (2 * E.TICKSIZE) * E.TICKVALUE * (decimal)volume;
      initialRisk = (decimal)wanted_opening_SL - (decimal)wanted_opening_price;
      if (initialRisk < 0) initialRisk = -initialRisk;
      money = E.TICKVALUE * (decimal)volume / E.TICKSIZE;
      initialRisk *= money;
      money *= (decimal)wanted_opening_price / E.AccountLeverage;

      if (opening_price != 0)
      {
        height = low = (decimal)opening_price;
      }
      else
      {
        height = 0;
        low = LocalConfig.INF;
      }
      new_height = new_low = false;
      SL_para = (decimal)SL;
      bad_slippage = E.getSlippage(histery_on_market: true);
      age_before_opening = 0;
      max_in_age_before_opening = -1;
      min_in_age_before_opening = LocalConfig.INF;
      imp_SR_for_strict = -1;
      bol_span = middleChart.Indicators["BE_Up"][0] - middleChart.Indicators["BE_Down"][0];
      if (bol_span == 0) bol_span = E.TICKSIZE;
      initial_VIM = middleChart.Indicators["VI_Max"][0];
      initial_StdDev = middleChart.Indicators["StdDev"][0];
      initial_StdDev_MA = middleChart.Indicators["StdDev_MA"][0];

      strict_last_close_SR_helper_isset = false;

      // do logowania, głównie debugowego
      opening_snapshot = Snapshot.snapshot;
    }

    /// <summary>
    /// Wydobądź identyfikator z komentarza (-1 gdy brak). Przydaje się do wykrywania pozycji po timeoucie.
    /// </summary>
    /// <param name="comment">Komentarz.</param>
    /// <returns>Identyfikator.</returns>
    public static string idFromComment(string comment)
    {
      string ret = "";
      string[] pom = comment.Split(' ');
      for (int i = 0; i < pom.Count() - 1; i++)
      {
        if (pom[i] == "N:")
        {
          ret = pom[i + 1];
          break;
        }
      }
      return ret;
    }

    /// <summary>
    /// Wydobądź numer poprzednika z komentarza (-1 gdy brak). Przydaje się do wykrywania pozycji po częściowym zamknięciu poprzednika.
    /// </summary>
    /// <param name="comment">Komentarz.</param>
    /// <returns>Numer poprzednika.</returns>
    public static int predFromComment(string comment)
    {
      // komentarz tak naprawde zmienia się na "split from #1234" (z odpowiednim numrem starego ticketu)
      string ret = "";
      int ret2 = -1;
      string[] pom = comment.Split(' ');
      for (int i = 0; i < pom.Count() - 1; i++)
      {
        if (pom[i] == "from" && pom[i + 1][0] == '#')
        {
          ret = pom[i + 1].Substring(1);
          ret2 = Convert.ToInt32(ret);
          break;
        }
      }
      return ret2;
    }

    /// <summary>
    /// Sprawdza czy przejście z obecnego statusu do nowego jest zgodne z regułami gry. Pomaga wychwycić ewentualne fuckupy (także po stronie brokera). Ta funkcja zawsze powinna zwrócić true.
    /// </summary>
    /// <param name="newStatus">Nowy status zlecenia.</param>
    /// <returns>Czy przejście z obecnego statusu do nowego jest zgodne z regułami gry.</returns>
    public bool isAllowedStatusChange(Enums.PositionStatus newStatus)
    {
      return Lib.statusToStatus[(int)status][(int)newStatus];
    }

    /// <summary>
    /// Uaktualnij successor w razie potrzeby (po uaktualnieniu księgowania po close(Position)). Tylko dla pozycji częściowo zachowanej (tej o większym wolumenie).
    /// </summary>
    /// <param name="suc_id">Numer pozycji porzedzającej w tym samym kierunku, częściowo zamkniętej przez close(Position).</param>
    private void setSuccessor(int suc_id)
    {
      successor = suc_id;
    }

    /// <summary>
    /// Stwórz obiekt pozycji po częściowym zamknięciu jakiejś pozycji (zmienia to ticket niestety, więc robimy nowy obiekt). Zwraca ten obiekt.
    /// </summary>
    /// <param name="old_pos">Stara pozycja.</param>
    /// <param name="wanted_volume">Oczekiwany rozmiar nowej pozycji.</param>
    /// <param name="_status">Status nowej pozycji.</param>
    /// <param name="OrderClosePrice">Cena zamknięcia pozycji.</param>
    /// <param name="OrderCloseTime">Czas zamknięcia pozycji.</param>
    /// <param name="OrderComment">Komentarz dowiązany do pozycji.</param>
    /// <param name="OrderCommission">Prowizja za pozycję.</param>
    /// <param name="OrderExpiration">Czas wygaśnięcia zlecenia otwierającego.</param>
    /// <param name="OrderLots">Rozmiar pozycji.</param>
    /// <param name="OrderMagicNumber">Magiczna liczba.</param>
    /// <param name="OrderOpenPrice">Cena otwarcia.</param>
    /// <param name="OrderOpenTime">Czas otwarcia,</param>
    /// <param name="OrderProfit">Zysk z pozycji.</param>
    /// <param name="OrderStopLoss">Poziom stop loss.</param>
    /// <param name="OrderSwap">Koszt swapowania pozycji.</param>
    /// <param name="OrderSymbol">Symbol waloru.</param>
    /// <param name="OrderTakeProfit">Poziom take profit.</param>
    /// <param name="OrderTicket">Identyfikator pozycji.</param>
    /// <param name="OrderType">Rodzaj zlecenia dotyczącego pozycji.</param>
    /// <returns>Obiekt pozycji powstałej po częściowym zamknięciu jakiejś.</returns>
    /// <exception cref="Exception">Przerwij działanie programu w przypadku poważnego błędu sygnalizowanego wyjątkiem.</exception>
    public static Position createSuccessor(Position old_pos, double wanted_volume, Enums.PositionStatus _status, double OrderClosePrice, DateTime OrderCloseTime, string OrderComment, double OrderCommission, DateTime OrderExpiration, double OrderLots, int OrderMagicNumber, double OrderOpenPrice, DateTime OrderOpenTime, double OrderProfit, double OrderStopLoss, double OrderSwap, string OrderSymbol, double OrderTakeProfit, int OrderTicket, TradeOperation OrderType)
    {
      /* Przy częściowym zamknięciu pozycji resztka starej ma nowy ticket i jest nową pozycją o parametrach:
       * 1. Wolumen to pozostały wolumen ze starej.
       * 2. Cena otwarcia taka jak cena otwarcia starej (chociaż naprawdę przeważnie jest inna).
       * 3. Czas otwarcia taki jak czas otwarcia starej (chociaż naprawdę przeważnie jest inny).
       * 4. Komentarz "from #oldTicketnumber".
       * 5. Zachowany magic number ze starej.
      */

      // sprawdzić parametry
      if (old_pos == null) throw new Exception("createSuccessor(): null passed as old_pos");
      if (wanted_volume != OrderLots) throw new Exception("createSuccessor(): OrderLots " + OrderLots + " instead of " + wanted_volume);
      //DateTime _creation_time = old_pos.creation_time;
      DateTime _creation_time = E.TIME;
      long _creation_tick = old_pos.creation_tick;
      Enums.LimitedDirection _dir = old_pos.dir;
      if ((_dir == Enums.LimitedDirection.Up && OrderType != TradeOperation.OP_BUY) || (_dir == Enums.LimitedDirection.Down && OrderType != TradeOperation.OP_SELL)) throw new Exception("createSuccessor(): old dir " + _dir + " but new operation is  " + OrderType);
      double _wanted_opening_price = old_pos.wanted_opening_price;
      int _allowed_opening_slippage = old_pos.allowed_opening_slippage;
      Enums.PositionTactics _tactics = old_pos.tactics;
      if (OrderMagicNumber != (int)old_pos.magic_num) throw new Exception("createSuccessor(): OrderMagicNumber " + OrderMagicNumber + " instead of " + old_pos.magic_num);
      if (OrderOpenPrice != old_pos.opening_price) throw new Exception("createSuccessor(): OrderOpenPrice " + OrderOpenPrice + " instead of " + old_pos.opening_price);
      // dla pozycji częściowo zamykanych wolumenem to opening time starej, a pozycją - closing time
      if (OrderOpenTime != old_pos.opening_time && OrderOpenTime != old_pos.closing_time) throw new Exception("createSuccessor(): OrderOpenTime " + OrderOpenTime + " instead of " + old_pos.opening_time + " or " + old_pos.closing_time);
      if (OrderStopLoss != old_pos.SL) throw new Exception("createSuccessor(): OrderStopLoss " + OrderStopLoss + " instead of " + old_pos.SL);
      if (OrderSymbol != old_pos.symbol) throw new Exception("createSuccessor(): OrderSymbol " + OrderSymbol + " instead of " + old_pos.symbol);
      if (OrderTakeProfit != old_pos.TP) throw new Exception("createSuccessor(): OrderTakeProfit " + OrderTakeProfit + " instead of " + old_pos.TP);

      Enums.PositionProfitStatus _profit_status;
      if (_dir == Enums.LimitedDirection.Up)
      {
        switch (Lib.comp(OrderStopLoss, OrderOpenPrice))
        {
          case 0:
            _profit_status = Enums.PositionProfitStatus.BreakEven;
            break;
          case -1:
            _profit_status = Enums.PositionProfitStatus.Losing;
            break;
          default:
            double pessimistic = OrderStopLoss - (double)E.getSlippage(histery_on_market: true);
            switch (Lib.comp(pessimistic, OrderOpenPrice))
            {
              case 0:
                _profit_status = Enums.PositionProfitStatus.StrongBreakEven;
                break;
              case -1:
                _profit_status = Enums.PositionProfitStatus.Prospective;
                break;
              default:
                _profit_status = Enums.PositionProfitStatus.Profitable;
                break;
            }
            break;
        }
      }
      else
      {
        switch (Lib.comp(OrderStopLoss, OrderOpenPrice))
        {
          case 0:
            _profit_status = Enums.PositionProfitStatus.BreakEven;
            break;
          case 1:
            _profit_status = Enums.PositionProfitStatus.Losing;
            break;
          default:
            double pessimistic = OrderStopLoss + (double)E.getSlippage(histery_on_market: true);
            switch (Lib.comp(pessimistic, OrderOpenPrice))
            {
              case 0:
                _profit_status = Enums.PositionProfitStatus.StrongBreakEven;
                break;
              case 1:
                _profit_status = Enums.PositionProfitStatus.Prospective;
                break;
              default:
                _profit_status = Enums.PositionProfitStatus.Profitable;
                break;
            }
            break;
        }
      }
      Enums.PositionProfitStatus _profit_status_volatile;
      if (_dir == Enums.LimitedDirection.Up)
      {
        switch (Lib.comp((double)E.BID, OrderOpenPrice))
        {
          case 0:
            _profit_status_volatile = Enums.PositionProfitStatus.BreakEven;
            break;
          case -1:
            _profit_status_volatile = Enums.PositionProfitStatus.Losing;
            break;
          default:
            double pessimistic = (double)E.BID - (double)E.getSlippage(histery_on_market: true);
            switch (Lib.comp(pessimistic, OrderOpenPrice))
            {
              case 0:
                _profit_status_volatile = Enums.PositionProfitStatus.StrongBreakEven;
                break;
              case -1:
                _profit_status_volatile = Enums.PositionProfitStatus.Prospective;
                break;
              default:
                _profit_status_volatile = Enums.PositionProfitStatus.Profitable;
                break;
            }
            break;
        }
      }
      else
      {
        switch (Lib.comp((double)E.ASK, OrderOpenPrice))
        {
          case 0:
            _profit_status_volatile = Enums.PositionProfitStatus.BreakEven;
            break;
          case 1:
            _profit_status_volatile = Enums.PositionProfitStatus.Losing;
            break;
          default:
            double pessimistic = (double)E.ASK + (double)E.getSlippage(histery_on_market: true);
            switch (Lib.comp(pessimistic, OrderOpenPrice))
            {
              case 0:
                _profit_status_volatile = Enums.PositionProfitStatus.StrongBreakEven;
                break;
              case 1:
                _profit_status_volatile = Enums.PositionProfitStatus.Prospective;
                break;
              default:
                _profit_status_volatile = Enums.PositionProfitStatus.Profitable;
                break;
            }
            break;
        }
      }
      Enums.MagicNumbers _OrderMagicNumber;
      if (Lib.isIntToMagicOk(OrderMagicNumber))
      {
        _OrderMagicNumber = (Enums.MagicNumbers)OrderMagicNumber;
      }
      else
      {
        // to się nie powinno stać
        _OrderMagicNumber = Enums.MagicNumbers.Aggressive;
        if (_tactics == Enums.PositionTactics.Peak) _OrderMagicNumber = Enums.MagicNumbers.Peak;
        else if (_tactics == Enums.PositionTactics.Strict) _OrderMagicNumber = Enums.MagicNumbers.Strict;
        Logger.Error("createSuccessor() had unknown magic number " + OrderMagicNumber + " so we had to artificially build " + _OrderMagicNumber);
      }
      Position ret = new Position(OrderTicket, _creation_time, _creation_tick, _dir, OrderType, OrderLots, _wanted_opening_price, _allowed_opening_slippage, OrderStopLoss, OrderTakeProfit, OrderExpiration, _tactics, OrderOpenPrice, OrderOpenTime, OrderStopLoss, OrderTakeProfit, old_pos.id, OrderClosePrice, OrderCloseTime, OrderCommission, OrderSwap, OrderProfit, _status, _profit_status, _profit_status_volatile, _OrderMagicNumber, OrderComment, true);
      if (_status == Enums.PositionStatus.Closed)
      {
        // SL, TP lub chuj-broker
        double p1 = (OrderTakeProfit >= OrderClosePrice ? OrderTakeProfit - OrderClosePrice : OrderClosePrice - OrderTakeProfit);
        double p2 = (OrderStopLoss >= OrderClosePrice ? OrderStopLoss - OrderClosePrice : OrderClosePrice - OrderStopLoss);
        double level_hit = OrderStopLoss;
        if (p2 >= p1) level_hit = OrderTakeProfit;
        if (Lib.comp(OrderStopLoss, OrderClosePrice) != 0 && Lib.comp(OrderTakeProfit, OrderClosePrice) != 0)
        {
          Logger.Warn("createSuccessor(): OrderClosePrice " + OrderClosePrice + " not S/L " + OrderStopLoss + " nor T/P " + OrderTakeProfit + "!");
        }
        ret.closing_slippage = half_normalize((ret.dir == Enums.LimitedDirection.Up ? (decimal)OrderClosePrice - (decimal)level_hit : (decimal)level_hit - (decimal)OrderClosePrice));
        ret.spread_costs += -E.spread / (2 * E.TICKSIZE) * E.TICKVALUE * (decimal)ret.volume;
        ret.slippage_costs += (E.POINT * ret.closing_slippage) / E.TICKSIZE * E.TICKVALUE * (decimal)ret.volume;
      }
      ret.opening_slippage = old_pos.opening_slippage;
      ret.slippage_costs += (E.POINT * old_pos.opening_slippage) / E.TICKSIZE * E.TICKVALUE * (decimal)ret.volume;
      old_pos.setSuccessor(ret.id);
      return ret;
    }

    /// <summary>
    /// Ukatualnij obiekt pozycji podczas księgowania nowymi parametrami. Zwraca true gdy wszystko ok, false gdy nie (i trzeba zamknąć pozycję i przerwać działanie programu).
    /// </summary>
    /// <param name="newStatus">Nowy status pozycji.</param>
    /// <param name="OrderClosePrice">Cena zamknięcia pozycji.</param>
    /// <param name="OrderCloseTime">Czas zamknięcia pozycji.</param>
    /// <param name="OrderComment">Komentarz dowiązany do pozycji.</param>
    /// <param name="OrderCommission">Prowizja za pozycję.</param>
    /// <param name="OrderExpiration">Czas wygaśnięcia zlecenia otwierającego.</param>
    /// <param name="OrderLots">Rozmiar pozycji.</param>
    /// <param name="OrderMagicNumber">Magiczna liczba.</param>
    /// <param name="OrderOpenPrice">Cena otwarcia.</param>
    /// <param name="OrderOpenTime">Czas otwarcia.</param>
    /// <param name="OrderProfit">Zysk z pozycji.</param>
    /// <param name="OrderStopLoss">Poziom stop loss.</param>
    /// <param name="OrderSwap">Koszt swapowania pozycji.</param>
    /// <param name="OrderSymbol">Symbol waloru.</param>
    /// <param name="OrderTakeProfit">Poziom take profit.</param>
    /// <param name="OrderTicket">Identyfikator pozycji.</param>
    /// <param name="OrderType">Rodzaj zlecenia dotyczącego pozycji.</param>
    /// <returns>Zwraca true gdy wszystko ok, false gdy nie (i trzeba zamknąć pozycję i przerwać działanie programu).</returns>
    /// <exception cref="Exception">Przerwij działanie programu w przypadku poważnego błędu sygnalizowanego wyjątkiem.</exception>
    public bool update(Enums.PositionStatus newStatus, double OrderClosePrice, DateTime OrderCloseTime, string OrderComment, double OrderCommission, DateTime OrderExpiration, double OrderLots, int OrderMagicNumber, double OrderOpenPrice, DateTime OrderOpenTime, double OrderProfit, double OrderStopLoss, double OrderSwap, string OrderSymbol, double OrderTakeProfit, int OrderTicket, TradeOperation OrderType)
    {
      bool ret = true;
      int m = (int)this.status;
      int n = (int)newStatus;
      string errors = "";
      string warnings = "";
      bool warn = false;
      if (isAllowedStatusChange(newStatus))
      {
        if (status == Enums.PositionStatus.Pending)
        {
          DateTime dt = creation_time;
          dt = dt.AddSeconds(-dt.Second);
          dt = dt.AddMilliseconds(-dt.Millisecond);
          int pom = dt.Minute / MiddleChartConfig.minutes_per_bar;
          pom *= MiddleChartConfig.minutes_per_bar;
          dt = dt.AddMinutes(pom - dt.Minute);
          TimeSpan ts = E.TIME - dt;
          double sec = ts.TotalSeconds;
          if (sec == 0) sec = 1;
          sec /= (60 * MiddleChartConfig.minutes_per_bar);
          age_before_opening = (int)sec;
          age_before_opening++;
          for (int i = 0; i < age_before_opening; i++)
          {
            decimal pom2 = middleChart.High(i);
            if (pom2 > max_in_age_before_opening) max_in_age_before_opening = pom2;
            pom2 = middleChart.Low(i);
            if (pom2 < min_in_age_before_opening) min_in_age_before_opening = pom2;
          }
          if (newStatus == Enums.PositionStatus.Opened)
          {
            // koniecznie uaktualniamy extremum_plus_strict
            int b1 = middleChart.findBar(OrderOpenTime);
            int b2 = middleChart.findBar(creation_time);
            if (b1 != -1 && b2 != -1)
            {
              if (dir == Enums.LimitedDirection.Up)
              {
                for (int i = b1; i <= b2; i++)
                {
                  if (middleChart.High(i) > extremum_plus_strict) extremum_plus_strict = middleChart.High(i);
                }
              }
              else
              {
                for (int i = b1; i <= b2; i++)
                {
                  if (middleChart.Low(i) < extremum_plus_strict) extremum_plus_strict = middleChart.Low(i);
                }
              }
            }
          }
        }
        if (status != newStatus)
        {
          Logger.Info("Position " + OrderTicket + " status changed from " + status + " to " + newStatus + " at " + E.TIME + " (" + E.tick_counter + ")");
          status = newStatus;
        }
      }
      else
      {
        errors += "Position " + OrderTicket + " status changed from " + status + " to " + newStatus + " (disallowed!) at " + E.TIME + " (" + E.tick_counter + ")";
        ret = false;
      }
      if (Lib.acceptedPositionChanges[m][n][0])
      {
        if (status == Enums.PositionStatus.Closed)
        {
          if (!Lib.isZero(wanted_closing_price))
          {
            closing_slippage = half_normalize((dir == Enums.LimitedDirection.Up ? (decimal)OrderClosePrice - (decimal)wanted_closing_price : (decimal)wanted_closing_price - (decimal)OrderClosePrice));
            // chcieliśmy zamknąć zleceniem close() lub closeby()
            if (Lib.comp(wanted_closing_price, OrderClosePrice) != 0)
            {
              // może poślizgu w dobrą stronę nie raportować?
              //bool exc = !Lib.isZero(OrderClosePrice - wanted_closing_price, normalize((decimal)allowed_closing_slippage * (decimal)E.POINT) + Lib.max_accuracy_double);
              // raczej nie
              bool exc = -closing_slippage > allowed_closing_slippage;
              if (closed_by == -1 && exc)
              {
                // podczas zamknięcia pozycją przeciwną w jednej z pozycji może wystąpić złe przesunięcie (kompensowane przez przesunięcie w drugiej)
                // więc raczej wchodzimy tu przy && niż ||
                warnings += "OrderClosePrice " + OrderClosePrice + " instead of " + wanted_closing_price + (exc ? " and slippage " + allowed_closing_slippage + " exceeded" : "") + "! ";
                warn = true;
              }
            }
          }
          else
          {
            // SL, TP lub chuj-broker
            double p1 = (OrderTakeProfit >= OrderClosePrice ? OrderTakeProfit - OrderClosePrice : OrderClosePrice - OrderTakeProfit);
            double p2 = (OrderStopLoss >= OrderClosePrice ? OrderStopLoss - OrderClosePrice : OrderClosePrice - OrderStopLoss);
            double level_hit = OrderStopLoss;
            if (p2 >= p1) level_hit = OrderTakeProfit;
            if (Lib.comp(OrderStopLoss, OrderClosePrice) != 0 && Lib.comp(OrderTakeProfit, OrderClosePrice) != 0)
            {
              warnings += "OrderClosePrice " + OrderClosePrice + " not S/L " + OrderStopLoss + " nor T/P " + OrderTakeProfit + "! ";
              warn = true;
            }
            closing_slippage = half_normalize((dir == Enums.LimitedDirection.Up ? (decimal)OrderClosePrice - (decimal)level_hit : (decimal)level_hit - (decimal)OrderClosePrice));
          }
          spread_costs += -E.spread / (2 * E.TICKSIZE) * E.TICKVALUE * (decimal)volume;
          slippage_costs += (E.POINT * closing_slippage) / E.TICKSIZE * E.TICKVALUE * (decimal)volume;
          if (dir == Enums.LimitedDirection.Up)
          {
            // zamiast POINT powinno być raczej TICKSIZE, ale na testach na jednym kopie nawet gdy była dokładność danych 0.00001 to ticksize pokazywało 0.0001 więc lepiej zostawmy point
            pips = (int)normalize(((decimal)OrderClosePrice - (decimal)OrderOpenPrice) / E.POINT);
            pips_pot = (int)normalize((height - (decimal)OrderOpenPrice) / E.POINT);
          }
          else
          {
            // zamiast POINT powinno być raczej TICKSIZE, ale na testach na jednym kopie nawet gdy była dokładność danych 0.00001 to ticksize pokazywało 0.0001 więc lepiej zostawmy point
            pips = (int)normalize(((decimal)OrderOpenPrice - (decimal)OrderClosePrice) / E.POINT);
            pips_pot = (int)normalize(((decimal)OrderOpenPrice - low) / E.POINT);
          }
        }
        this.closing_price = OrderClosePrice;
      }
      else
      {
        if (Lib.comp(this.closing_price, OrderClosePrice) != 0)
        {
          errors += "OrderClosePrice " + OrderClosePrice + " instead of " + this.closing_price + "! ";
          ret = false;
          this.closing_price = OrderClosePrice;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][1])
      {
        this.closing_time = OrderCloseTime;
      }
      else
      {
        if (this.closing_time != OrderCloseTime)
        {
          errors += "OrderCloseTime " + OrderCloseTime + " instead of " + this.closing_time + "! ";
          ret = false;
          this.closing_time = OrderCloseTime;
        }
      }
      // komentarz może np mieć dodany "from #1234" gdy częściowo zamniemy albo zmienić się na "expiration" gdy zlecenie pending wygaśnie albo na "cancelled" gdy zostanie skasowane
      if (Lib.acceptedPositionChanges[m][n][2])
      {
        if (!this.opening_comment.Equals(OrderComment, StringComparison.Ordinal))
        {
          if (OrderComment.Equals("expiration", StringComparison.Ordinal))
          {
            Logger.Info("Position " + OrderTicket + " expired at " + E.TIME + " (" + E.tick_counter + ")");
            ChartLogger.LogExpiring(mt4, OrderTicket);
          }
          else if (OrderComment.Equals("cancelled", StringComparison.Ordinal))
          {
            Logger.Info("Position " + OrderTicket + " was cancelled at " + E.TIME + " (" + E.tick_counter + ")");
          }
          else if (OrderComment.Equals(this.opening_comment + "[sl]", StringComparison.Ordinal))
          {
            Logger.Info("Position " + OrderTicket + " hit S/L at " + E.TIME + " (" + E.tick_counter + ")");
            ChartLogger.LogHittingSL(mt4, OrderTicket);
          }
          else if (OrderComment.Equals(this.opening_comment + "[tp]", StringComparison.Ordinal))
          {
            Logger.Info("Position " + OrderTicket + " hit T/P at " + E.TIME + " (" + E.tick_counter + ")");
            ChartLogger.LogHittingTP(mt4, OrderTicket);
          }
          else if (OrderComment.Equals("partial close", StringComparison.Ordinal))
          {
            int x = Lib.comp(this.commission, OrderCommission);
            string s = "Position " + OrderTicket + " partially closed at " + E.TIME + " (" + E.tick_counter + ")";
            bool prob = false;
            if (x == 1)
            {
              prob = true;
              s += " and commission changed from " + this.commission + " to " + OrderCommission;
            }
            if (Lib.comp(this.volume, OrderLots) < 0)
            {
              // mogą być równe gdy zamykamy częściowo przy użyciu innej pozycji
              prob = true;
              s += " and volume changed from " + this.volume + " to " + OrderLots;
            }
            if (prob)
            {
              Logger.Warn(s + "!");
            }
            else
            {
              Logger.Info(s);
            }
          }
          else if (OrderComment.StartsWith("close hedge by "))
          {
            Logger.Info("Position " + OrderTicket + " closed hedge with " + closed_by + " at " + E.TIME + " (" + E.tick_counter + ")");
          }
          else
          {
            warnings += "OrderComment changed from \"" + this.opening_comment + "\" to \"" + OrderComment + "\"! ";
            warn = true;
          }
          this.opening_comment = OrderComment;
          id_from_comment = Position.idFromComment(opening_comment);
        }
      }
      else
      {
        if (!this.opening_comment.Equals(OrderComment, StringComparison.Ordinal))
        {
          errors += "OrderComment \"" + OrderComment + "\" instead of \"" + this.opening_comment + "\"! ";
          ret = false;
          this.opening_comment = OrderComment;
          id_from_comment = Position.idFromComment(opening_comment);
        }
      }
      if (Lib.acceptedPositionChanges[m][n][3])
      {
        if (Lib.comp(this.commission, OrderCommission) > 0 && ever_updated)
        {
          if (n != 2 || m > 1)
          {
            warnings += "OrderCommission " + OrderCommission + " increased from " + this.commission + "! ";
            warn = true;
          }
        }
        this.commission = OrderCommission;
      }
      else
      {
        if (Lib.comp(this.commission, OrderCommission) != 0)
        {
          errors += "OrderCommission " + OrderCommission + " instead of " + this.commission + "! ";
          ret = false;
          this.commission = OrderCommission;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][4] || !ever_updated)
      {
        if (this.opening_expiration != OrderExpiration)
        {
          warnings += "OrderExpiration " + OrderExpiration + " instead of " + this.opening_expiration + "! ";
          warn = true;
          this.opening_expiration = OrderExpiration;
        }
      }
      else
      {
        if (this.opening_expiration != OrderExpiration)
        {
          errors += "OrderExpiration " + OrderExpiration + " instead of " + this.opening_expiration + "! ";
          ret = false;
          this.opening_expiration = OrderExpiration;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][5])
      {
        int x = Lib.comp(this.volume, OrderLots);
        if (x == 1)
        {
          if (!OrderComment.Equals("partial close", StringComparison.Ordinal) && !OrderComment.StartsWith("close hedge by "))
          {
            errors += "OrderLots " + OrderLots + " instead of " + this.volume + "! ";
            ret = false;
          }
        }
        else if (x == -1)
        {
          errors += "OrderLots " + OrderLots + " instead of " + this.volume + "! ";
          ret = false;
        }
        this.volume = OrderLots;
      }
      else
      {
        if (Lib.comp(this.volume, OrderLots) != 0)
        {
          errors += "OrderLots " + OrderLots + " instead of " + this.volume + "! ";
          ret = false;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][6])
      {
        throw new Exception("magic number change allowed for " + m + " -> " + n);
      }
      else
      {
        if ((int)this.magic_num != OrderMagicNumber)
        {
          errors += "OrderMagicNumber " + OrderMagicNumber + " instead of " + ((int)this.magic_num) + "! ";
          ret = false;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][7] || !ever_updated)
      {
        opening_slippage = half_normalize((dir == Enums.LimitedDirection.Down ? (decimal)OrderOpenPrice - (decimal)wanted_opening_price : (decimal)wanted_opening_price - (decimal)OrderOpenPrice));
        if (Lib.comp(wanted_opening_price, OrderOpenPrice) != 0 && !ever_updated)
        {
          // raczej poślizgu w dobrą stronę nie raportować
          //bool exc = !Lib.isZero(OrderOpenPrice - wanted_opening_price, normalize((decimal)allowed_opening_slippage * (decimal)E.POINT) + Lib.max_accuracy_double);
          bool exc = -opening_slippage > allowed_opening_slippage;
          warnings += "OrderOpenPrice " + OrderOpenPrice + " instead of " + wanted_opening_price + (exc ? " and slippage " + allowed_opening_slippage + " exceeded" : "") + "! ";
          warn = true;
        }
        slippage_costs += (E.POINT * opening_slippage) / E.TICKSIZE * E.TICKVALUE * (decimal)OrderLots;
        this.opening_price = OrderOpenPrice;
      }
      else
      {
        if (Lib.comp(this.opening_price, OrderOpenPrice) != 0)
        {
          errors += "OrderOpenPrice " + OrderOpenPrice + " instead of " + this.opening_price + "! ";
          ret = false;
          this.opening_price = OrderOpenPrice;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][8] || !ever_updated)
      {
        this.opening_time = OrderOpenTime;
      }
      else
      {
        if (this.opening_time != OrderOpenTime)
        {
          errors += "OrderOpenTime " + OrderOpenTime + " instead of " + this.opening_time + "! ";
          ret = false;
          this.opening_time = OrderOpenTime;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][9])
      {
        this.profit = OrderProfit;
      }
      else
      {
        if (Lib.comp(this.profit, OrderProfit) != 0)
        {
          errors += "OrderProfit " + OrderProfit + " instead of " + this.profit + "! ";
          ret = false;
          this.profit = OrderProfit;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][10])
      {
        this.SL = OrderStopLoss;
      }
      else
      {
        if (Lib.comp(this.SL, OrderStopLoss) != 0)
        {
          errors += "OrderStopLoss " + OrderStopLoss + " instead of " + this.SL + "! ";
          ret = false;
          this.SL = OrderStopLoss;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][11])
      {
        this.swap = OrderSwap;
      }
      else
      {
        if (Lib.comp(this.swap, OrderSwap) != 0)
        {
          errors += "OrderSwap " + OrderSwap + " instead of " + this.swap + "! ";
          ret = false;
          this.swap = OrderSwap;
        }
      }
      if (!this.symbol.Equals(OrderSymbol, StringComparison.Ordinal))
      {
        throw new Exception("OrderSymbol changed from \"" + this.symbol + "\" to \"" + OrderSymbol + "\"! ");
      }
      if (Lib.acceptedPositionChanges[m][n][13])
      {
        this.TP = OrderTakeProfit;
      }
      else
      {
        if (Lib.comp(this.TP, OrderTakeProfit) != 0)
        {
          errors += "OrderTakeProfit " + OrderTakeProfit + " instead of " + this.TP + "! ";
          ret = false;
          this.TP = OrderTakeProfit;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][14])
      {
        warnings += "Assigning id " + OrderTicket + " to unknown position! ";
        warn = false;
        this.id = OrderTicket;
      }
      else
      {
        if (this.id != OrderTicket)
        {
          errors += "OrderTicket " + OrderTicket + " instead of " + this.id + "! ";
          ret = false;
        }
      }
      if (Lib.acceptedPositionChanges[m][n][15])
      {
        if (this.current_operation != OrderType)
        {
          bool all = false;
          if (this.current_operation == TradeOperation.OP_BUYLIMIT || this.current_operation == TradeOperation.OP_BUYSTOP)
          {
            all = OrderType == TradeOperation.OP_BUY;
          }
          if (this.current_operation == TradeOperation.OP_SELLLIMIT || this.current_operation == TradeOperation.OP_SELLSTOP)
          {
            all = OrderType == TradeOperation.OP_SELL;
          }
          if (all) this.current_operation = OrderType;
          else
          {
            errors += "OrderType " + OrderType + " obtained from " + this.current_operation + "! ";
            ret = false;
          }
        }
      }
      else
      {
        if (this.current_operation != OrderType)
        {
          errors += "OrderType " + OrderType + " instead of " + this.current_operation + "! ";
          ret = false;
        }
      }

      total_profit = commission + swap + profit;
      //if (E.tick_counter == 839022) Console.WriteLine("KURRRRRRRRRRWA A " + price_per_profit_unit + " " + price_for_costs_normalized);

      if (!Lib.isZero(profit))
      {
        decimal p = (decimal)opening_price;
        if (dir == Enums.LimitedDirection.Up)
        {
          p -= E.BID;
        }
        else
        {
          p -= E.ASK;
        }
        price_per_profit_unit = p / (decimal)profit;
        //if (E.tick_counter == 839022) Console.WriteLine("KURRRRRRRRRRWA B " + p + " " + price_per_profit_unit);
        if (price_per_profit_unit < 0) price_per_profit_unit = -price_per_profit_unit;
      }
      if (!Lib.isZero(price_per_profit_unit))
      {
        // co prawda jeśli profit jest zerowy to nie mamy dokładnej wartości tylko przybliżoną na podstawie ostatnio znanej informacji, ale za to waluta depozytowa to nie musi być bazowa ani kwotowana i nie musimy uwzględniać dodatkowego kursu
        price_for_commission = (double)((decimal)commission * price_per_profit_unit);
        price_for_swap = (double)((decimal)swap * price_per_profit_unit);
        decimal pom = ((decimal)swap + (decimal)commission) * price_per_profit_unit;
        price_for_costs = (double)pom; // uwzględniamy cud: teoretycznie swap czasem może być dodatni
        price_for_costs_normalized = (double)((decimal)Math.Floor((double)((decimal)price_for_costs / E.POINT)) * E.POINT);
        //if (E.tick_counter == 839022) Console.WriteLine("KURRRRRRRRRRWA C " + price_for_commission + " " + price_for_swap + " " + pom + " " + price_for_costs + " " + price_for_costs_normalized);
      }
      //if (E.tick_counter == 839022) Console.WriteLine("KURRRRRRRRRRWA D " + price_for_costs_normalized + " " + (opening_price + price_for_costs_normalized));
      if (dir == Enums.LimitedDirection.Up)
      {
        break_even = opening_price - price_for_costs_normalized;
        switch (Lib.comp(SL, break_even))
        {
          case 0:
            if (profit_status != Enums.PositionProfitStatus.BreakEven)
            {
              Console.WriteLine("" + E.TIME + " (" + E.tick_counter + ") " + "\tposition " + id + " update(): reached BreakEven " + break_even + " open " + opening_price + " open_SL " + wanted_opening_SL + " min " + ((decimal)low) + " max " + ((decimal)height));
            }
            profit_status = Enums.PositionProfitStatus.BreakEven;
            break;
          case -1:
            profit_status = Enums.PositionProfitStatus.Losing;
            break;
          default:
            double pessimistic = SL - (double)E.getSlippage(histery_on_market: true);
            switch (Lib.comp(pessimistic, break_even))
            {
              case 0:
                if (profit_status != Enums.PositionProfitStatus.StrongBreakEven)
                {
                  Console.WriteLine("" + E.TIME + " (" + E.tick_counter + ") " + "\tposition " + id + " update(): reached StrongBreakEven " + SL + " open " + opening_price + " open_SL " + wanted_opening_SL + " min " + ((decimal)low) + " max " + ((decimal)height));
                }
                profit_status = Enums.PositionProfitStatus.StrongBreakEven;
                break;
              case -1:
                if (profit_status != Enums.PositionProfitStatus.Prospective)
                {
                  Console.WriteLine("" + E.TIME + " (" + E.tick_counter + ") " + "\tposition " + id + " update(): reached Prospective " + SL + " open " + opening_price + " open_SL " + wanted_opening_SL + " min " + ((decimal)low) + " max " + ((decimal)height));
                }
                profit_status = Enums.PositionProfitStatus.Prospective;
                break;
              default:
                if (profit_status != Enums.PositionProfitStatus.Profitable)
                {
                  Console.WriteLine("" + E.TIME + " (" + E.tick_counter + ") " + "\tposition " + id + " update(): reached Profitable " + SL + " open " + opening_price + " open_SL " + wanted_opening_SL + " min " + ((decimal)low) + " max " + ((decimal)height));
                }
                profit_status = Enums.PositionProfitStatus.Profitable;
                break;
            }
            break;
        }
      }
      else
      {
        break_even = opening_price + price_for_costs_normalized;
        switch (Lib.comp(SL, break_even))
        {
          case 0:
            if (profit_status != Enums.PositionProfitStatus.BreakEven)
            {
              Console.WriteLine("" + E.TIME + " (" + E.tick_counter + ") " + "\tposition " + id + " update(): reached BreakEven " + break_even + " open " + opening_price + " open_SL " + wanted_opening_SL + " min " + ((decimal)low) + " max " + ((decimal)height));
            }
            profit_status = Enums.PositionProfitStatus.BreakEven;
            break;
          case 1:
            profit_status = Enums.PositionProfitStatus.Losing;
            break;
          default:
            double pessimistic = SL + (double)E.getSlippage(histery_on_market: true);
            switch (Lib.comp(pessimistic, break_even))
            {
              case 0:
                if (profit_status != Enums.PositionProfitStatus.StrongBreakEven)
                {
                  Console.WriteLine("" + E.TIME + " (" + E.tick_counter + ") " + "\tposition " + id + " update(): reached StrongBreakEven " + SL + " open " + opening_price + " open_SL " + wanted_opening_SL + " min " + ((decimal)low) + " max " + ((decimal)height));
                }
                profit_status = Enums.PositionProfitStatus.StrongBreakEven;
                break;
              case 1:
                if (profit_status != Enums.PositionProfitStatus.Prospective)
                {
                  Console.WriteLine("" + E.TIME + " (" + E.tick_counter + ") " + "\tposition " + id + " update(): reached Prospective " + SL + " open " + opening_price + " open_SL " + wanted_opening_SL + " min " + ((decimal)low) + " max " + ((decimal)height));
                }
                profit_status = Enums.PositionProfitStatus.Prospective;
                break;
              default:
                if (profit_status != Enums.PositionProfitStatus.Profitable)
                {
                  Console.WriteLine("" + E.TIME + " (" + E.tick_counter + ") " + "\tposition " + id + " update(): reached Profitable " + SL + " open " + opening_price + " open_SL " + wanted_opening_SL + " min " + ((decimal)low) + " max " + ((decimal)height));
                }
                profit_status = Enums.PositionProfitStatus.Profitable;
                break;
            }
            break;
        }
      }
      decimal price_c;
      if (dir == Enums.LimitedDirection.Up)
      {
        price_c = E.BID;
        switch (Lib.comp((double)E.BID, break_even))
        {
          case 0:
            profit_status_volatile = Enums.PositionProfitStatus.BreakEven;
            break;
          case -1:
            profit_status_volatile = Enums.PositionProfitStatus.Losing;
            break;
          default:
            double pessimistic = (double)E.BID - (double)E.getSlippage(histery_on_market: true);
            switch (Lib.comp(pessimistic, break_even))
            {
              case 0:
                profit_status_volatile = Enums.PositionProfitStatus.StrongBreakEven;
                break;
              case -1:
                profit_status_volatile = Enums.PositionProfitStatus.Prospective;
                break;
              default:
                profit_status_volatile = Enums.PositionProfitStatus.Profitable;
                break;
            }
            break;
        }
      }
      else
      {
        price_c = E.ASK;
        switch (Lib.comp((double)E.ASK, break_even))
        {
          case 0:
            profit_status_volatile = Enums.PositionProfitStatus.BreakEven;
            break;
          case 1:
            profit_status_volatile = Enums.PositionProfitStatus.Losing;
            break;
          default:
            double pessimistic = (double)E.ASK + (double)E.getSlippage(histery_on_market: true);
            switch (Lib.comp(pessimistic, break_even))
            {
              case 0:
                profit_status_volatile = Enums.PositionProfitStatus.StrongBreakEven;
                break;
              case 1:
                profit_status_volatile = Enums.PositionProfitStatus.Prospective;
                break;
              default:
                profit_status_volatile = Enums.PositionProfitStatus.Profitable;
                break;
            }
            break;
        }
      }

      maxLossFromOpening = calcPossibleLoss(from_current_price: false);
      maxLossFromCurrent = calcPossibleLoss(from_current_price: true);

      new_height = new_low = false;
      if (price_c < low)
      {
        low = price_c;
        new_low = true;
      }
      else if (price_c > height)
      {
        height = price_c;
        new_height = true;
      }

      bad_slippage = E.getSlippage(histery_on_market: true);

      last_updated_tick = E.tick_counter;

      if (ret) ever_updated = true;
      if (warn) Logger.Warn("update(): (" + E.TIME + ", " + E.tick_counter + ") parameters check while updating " + id + " revealed minor issue(s): " + warnings);
      if (!ret) Logger.Error("update(): (" + E.TIME + ", " + E.tick_counter + ") parameters check while updating " + id + " revealed huge problem(s): " + errors);
      return ret;
    }

    /// <summary>
    /// Potencjalna pesymistyczna strata na tej pozycji (zatrzymanie z poślizgiem na S/L) w walucie depozytowej rachunku.
    /// </summary>
    /// <param name="from_current_price">Liczona od ceny aktualnej a nie od ceny otwarcia.</param>
    /// <returns>Potencjalna pesymistyczna strata na tej pozycji (zatrzymanie z poślizgiem na S/L) w walucie depozytowej rachunku.</returns>
    private decimal calcPossibleLoss(bool from_current_price)
    {
      if (status == Enums.PositionStatus.Closed || status == Enums.PositionStatus.Deleted) return 0;
      decimal cur_pr = (decimal)opening_price;
      decimal pessimistic_SL = (decimal)SL;
      decimal ret = 0;
      //decimal pips_val = (decimal)volume * E.LOTSIZE * E.TICKSIZE; // wartość pipsa w walucie kwotowanej (wartość zlecenia * minimalny krok notowania)
      decimal pips_val = (decimal)volume * E.TICKVALUE; // wartość pipsa w walucie depozytowej dla tej pozycji

      if (dir == Enums.LimitedDirection.Up)
      {
        if (from_current_price) cur_pr = E.BID;
        cur_pr = cur_pr - (decimal)price_for_costs_normalized;
        pessimistic_SL -= E.getSlippage(histery_on_market: true);
        if (Lib.comp(pessimistic_SL, 0) <= 0) pessimistic_SL = E.TICKSIZE;
        if (Lib.comp((double)pessimistic_SL, (double)cur_pr) == -1)
        {
          ret = cur_pr - pessimistic_SL; // taka efektywna obsuwa w kursie może się zdażyć (taką stratę możemy ponieść)

          // to jest pesymistyczna poprawka na wartość pipsa uwzględniająca niekorzystne zmiany kursów
          decimal poprawka = 1; // waluta depozytowa jest jednocześnie walutą kwotowaną
#pragma warning disable 0162
          switch (InitConfig.DepositCurrency)
          {
            case Enums.CurrencyType.Base:
              // waluta depozytowa jest jednocześnie walutą bazową
              poprawka = (1 + (E.BID - pessimistic_SL) / pessimistic_SL);
              break;
            case Enums.CurrencyType.Deposit:
              // waluta depozytowa nie jest walutą ani bazową ani kwotowaną
              poprawka = (1 + (E.BID - pessimistic_SL) / pessimistic_SL);
              poprawka *= poprawka;
              break;
          }
#pragma warning restore 0162
          /*if (E.tick_counter == 6651 || E.tick_counter == 15400)
          {
            Logger.Info("KURWA " + from_current_price + " " + cur_pr + " " + pessimistic_SL + " " + ret + " " + poprawka + " " + pips_val);
          }*/
          pips_val *= poprawka; // nieco zawyżona wartość pipsa (przy niekorzystnych kursach) w walucie depozytowej
          ret *= pips_val;
          ret /= E.TICKSIZE;
        }
      }
      else
      {
        if (from_current_price) cur_pr = E.ASK;
        cur_pr = cur_pr + (decimal)price_for_costs_normalized;
        pessimistic_SL += E.getSlippage(histery_on_market: true);
        if (Lib.comp((double)pessimistic_SL, (double)cur_pr) == 1)
        {
          ret = pessimistic_SL - cur_pr; // taka efektywna obsuwa w kursie może się zdażyć (taką stratę możemy ponieść)

          // to jest pesymistyczna poprawka na wartość pipsa uwzględniająca niekorzystne zmiany kursów
          decimal poprawka = 1; // waluta depozytowa jest jednocześnie walutą kwotowaną
#pragma warning disable 0162
          switch (InitConfig.DepositCurrency)
          {
            case Enums.CurrencyType.Base:
              // waluta depozytowa jest jednocześnie walutą bazową
              poprawka = (1 + (pessimistic_SL - E.ASK) / pessimistic_SL);
              break;
            case Enums.CurrencyType.Deposit:
              // waluta depozytowa nie jest walutą ani bazową ani kwotowaną
              poprawka = (1 + (pessimistic_SL - E.ASK) / pessimistic_SL);
              poprawka *= poprawka;
              break;
          }
#pragma warning restore 0162
          pips_val *= poprawka; // nieco zawyżona wartość pipsa (przy niekorzystnych kursach) w walucie depozytowej
          ret *= pips_val;
          ret /= E.TICKSIZE;
        }
      }

      return ret;
    }

    /// <summary>
    /// Składa zlecenie. Zwraca numer zlecenia (>0) gdy sukces, -1 gdy porażka, -2 gdy nie wiadomo (trzeba odczekać i sprawdzić czy zlecenie ostatecznie weszło czy nie).
    /// </summary>
    /// <param name="cmd">Rodzaj zlecenia.</param>
    /// <param name="volume">Rozmiar zlecenia.</param>
    /// <param name="price">Cena otwarcia zlecenia..</param>
    /// <param name="slippage">Dopuszczalny poślizg przy otwarciu (będzie przeliczony na punkty i zaokrąglony w dół).</param>
    /// <param name="stoploss">Poziom stoploss.</param>
    /// <param name="takeprofit">Poziom takeprofit.</param>
    /// <param name="magic">Magic Number (nieunikatowa etykietka zlecenia).</param>
    /// <param name="comment">Opcjonalny opis zlecenia.</param>
    /// <param name="expiration">Data wygasnięcia zlecenia oczekującego.</param>
    /// <returns>Numer zlecenia (>0) gdy sukces, -1 gdy porażka, -2 gdy nie wiadomo (trzeba odczekać i sprawdzić czy zlecenie ostatecznie weszło czy nie)</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    private static int orderSend(TradeOperation cmd, double _volume, double _price, int _slippage, double _stoploss, double _takeprofit, Enums.MagicNumbers magic, string comment, DateTime expiration)
    {
      int _magic = (int)magic;
      //if (expiration == new DateTime()) expiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

      DateTime _creation_time_local_beginning = DateTime.UtcNow;

      int a = 0;
      string params_str = "" + InitConfig.Allowed_Symbol + " " + cmd + " " + _volume + " " + _price + " " + _slippage + " " + _stoploss + " " + _takeprofit + " " + expiration + " " + _magic + " " + comment;

      while (true)
      {
        a = -1;
        if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
        {
          break;
        }

        while (!mt4.IsConnected())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("No connection in orderSend(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsConnected())
        {
          a = -1;
          Logger.Error("No connection, giving up\n");
          break;
        }

        while (mt4.IsStopped())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trading stopped in orderSend(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (mt4.IsStopped())
        {
          a = -1;
          Logger.Error("Trading stopped, giving up\n");
          break;
        }

        // NB: gdyby było kilka EA działających jednocześnie, trzeba by używać semaforów (np. w postaci zmiennych globalnych wewnątrz MT4)
        while (!mt4.IsTradeAllowed())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trade not allowed in orderSend(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsTradeAllowed())
        {
          a = -1;
          Logger.Error("Trade not allowed, giving up\n");
          break;
        }

        try
        {
          mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
          Console.WriteLine("Making request OrderSend() with params: " + params_str);
          // TODO: może jakieś kolorki przy otwieraniu pozycji na rysunku (zależne od taktyki)
          _sw.Restart();
          a = mt4.OrderSend(InitConfig.Allowed_Symbol, cmd, _volume, _price, _slippage, _stoploss, _takeprofit, comment, _magic, expiration, new Color());
          if (a < 0)
          {
            // pytanie czy w ogóle tu kiedykolwiek wejdziemy....
            int err_no = mt4.GetLastError();
            string str = "OrderSend() failed (in " + _sw.ElapsedMilliseconds + "ms), error " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
            Logger.Error(str);
            System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
            // pewnie będzie powtórka bo nie wychodzimy z pętli
            // na przykład dla ERR_BROKER_BUSY (137) wypada powtórzyć, ale podobno ten błąd się nie pojawia
            // dla innych mam nadzieję, że raczej będzie rzucony wyjątek bo czasem nie ma sensu powtarzać
          }
          else
          {
            Console.WriteLine("OrderSend() made successfull request (in " + _sw.ElapsedMilliseconds + "ms) with params: " + params_str);
          }
        }
        catch (ErrInvalidFunctionParamvalue e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrCustomIndicatorError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrStringParameterExpected e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrIntegerParameterExpected e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrUnknownSymbol e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidPriceParam e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeNotAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrLongsNotAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrShortsNotAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrCommonError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidTradeParameters e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrServerBusy e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOldVersion e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrNoConnection e)
        {
          // trochę dłuższa przerwa; może użyć IsConnected()
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (!mt4.IsConnected())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("No connection, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (!mt4.IsConnected())
          {
            a = -1;
            Logger.Error("No connection, giving up\n");
            break;
          }
        }
        catch (ErrTooFrequentRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrAccountDisabled e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidAccount e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTimeout e)
        {
          // dłuższa przerwa; najpierw koniecznie sprawdzić czy pozycja jednak nie została otwarta!
          // tak naprawdę odpuszczamy od razu, sprawdzamy księgowanie pozycji i jeśli nie ma nowej, to przez ORDER_CHECK_DELAY nie otwieramy kolejnej tylko sprawdzamy księgowanie czy jednak się nie pojawi
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up, will check if position opened after all\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrInvalidPrice e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidStops e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidTradeVolume e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrMarketClosed e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeDisabled e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrNotEnoughMoney e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrPriceChanged e)
        {
          // trochę dłuższa przerwa, może uda się wpakować zlecenie, nie bawimy się w RefreshRates() i uaktualnianie wszystkiego
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOffQuotes e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrRequote e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrOrderLocked e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrLongPositionsOnlyAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTooManyRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeTimeout2 e)
        {
          // dłuższa przerwa; najpierw koniecznie sprawdzić czy pozycja jednak nie została otwarta!
          // tak naprawdę odpuszczamy od razu, sprawdzamy księgowanie pozycji i jeśli nie ma nowej, to przez ORDER_CHECK_DELAY nie otwieramy kolejnej tylko sprawdzamy księgowanie czy jednak się nie pojawi
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up, will check if position opened after all\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout3 e)
        {
          // dłuższa przerwa; najpierw koniecznie sprawdzić czy pozycja jednak nie została otwarta!
          // tak naprawdę odpuszczamy od razu, sprawdzamy księgowanie pozycji i jeśli nie ma nowej, to przez ORDER_CHECK_DELAY nie otwieramy kolejnej tylko sprawdzamy księgowanie czy jednak się nie pojawi
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up, will check if position opened after all\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout4 e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrTradeModifyDenied e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeContextBusy e)
        {
          // trochę dłuższa przerwa; IsTradeContextBusy() musi zwrócić najpierw false
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (mt4.IsTradeContextBusy())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("Trade context busy, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (mt4.IsTradeContextBusy())
          {
            a = -1;
            Logger.Error("Trade context busy, giving up\n");
            break;
          }
        }
        catch (ErrTradeExpirationDenied e)
        {
          // przerwać działanie programu (inna wersja: wyzerować ten parametr i samemu odwoływać niezrealizowane zlecenia po pewnym czasie)
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTooManyOrders e)
        {
          // odpuszczamy od razu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (Exception e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderSend():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        if (a > 0) break;
      }
      mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
      return a;
    }

    /// <summary>
    /// Modyfikuje oczekujące lub zrealizowane zlecenie. Zwraca 1 gdy sukces, -1 gdy porażka.
    /// </summary>
    /// <param name="ticket">Numer zlecenia.</param>
    /// <param name="price">Nowa cena otwarcia zlecenia oczekującego.</param>
    /// <param name="stoploss">Nowy poziom stoploss.</param>
    /// <param name="takeprofit">Nowy poziom takeprofit.</param>
    /// <param name="expiration">Nowa data wygasnięcia zlecenia oczekującego.</param>
    /// <returns>1 gdy sukces, -1 gdy porażka, -2 gdy nie wiemy</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    private static int orderModify(int ticket, double _price, double _stoploss, double _takeprofit, DateTime expiration = new DateTime())
    {
      DateTime _creation_time_local_beginning = DateTime.UtcNow;
      if (expiration == new DateTime()) expiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

      int a = -1;
      bool b = false;
      string params_str = "" + InitConfig.Allowed_Symbol + " " + ticket + " " + _price + " " + _stoploss + " " + _takeprofit + " " + expiration;

      while (true)
      {
        a = -1;
        if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
        {
          break;
        }

        while (!mt4.IsConnected())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("No connection in orderModify(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsConnected())
        {
          a = -1;
          Logger.Error("No connection, giving up\n");
          break;
        }

        while (mt4.IsStopped())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trading stopped in orderModify(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (mt4.IsStopped())
        {
          a = -1;
          Logger.Error("Trading stopped, giving up\n");
          break;
        }

        // NB: gdyby było kilka EA działających jednocześnie, trzeba by używać semaforów (np. w postaci zmiennych globalnych wewnątrz MT4)
        while (!mt4.IsTradeAllowed())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trade not allowed in orderModify(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsTradeAllowed())
        {
          a = -1;
          Logger.Error("Trade not allowed, giving up\n");
          break;
        }

        try
        {
          mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
          Console.WriteLine("Making request OrderModify() with params: " + params_str);
          //Console.WriteLine("KURWA: " + System.Environment.StackTrace);
          //System.Environment.Exit(1);
          // TODO: może jakieś kolorki (np. zachować poprzednie)
          _sw.Restart();
          b = mt4.OrderModify(ticket, _price, _stoploss, _takeprofit, expiration, new Color());
          if (b)
          {
            a = 1;
            Console.WriteLine("OrderModify() made successfull request (in " + _sw.ElapsedMilliseconds + "ms) with params: " + params_str);
          }
          else
          {
            // pytanie czy w ogóle tu kiedykolwiek wejdziemy....
            a = -1;
            int err_no = mt4.GetLastError();
            string str = "OrderModify() failed (in " + _sw.ElapsedMilliseconds + "ms), error " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
            Logger.Error(str);
            System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
            // pewnie będzie powtórka bo nie wychodzimy z pętli
            // na przykład dla ERR_BROKER_BUSY (137) wypada powtórzyć, ale podobno ten błąd się nie pojawia
            // dla innych mam nadzieję, że raczej będzie rzucony wyjątek bo czasem nie ma sensu powtarzać
          }
        }
        catch (ErrInvalidFunctionParamvalue e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrCustomIndicatorError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrIntegerParameterExpected e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrUnknownSymbol e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidPriceParam e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeNotAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrCommonError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidTradeParameters e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrServerBusy e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOldVersion e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrNoConnection e)
        {
          // trochę dłuższa przerwa; może użyć IsConnected()
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (!mt4.IsConnected())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("No connection, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (!mt4.IsConnected())
          {
            a = -1;
            Logger.Error("No connection, giving up\n");
            break;
          }
        }
        catch (ErrTooFrequentRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrAccountDisabled e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidAccount e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTimeout e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrInvalidPrice e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidStops e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidTradeVolume e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrMarketClosed e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeDisabled e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrNotEnoughMoney e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrPriceChanged e)
        {
          // trochę dłuższa przerwa, może uda się wpakować zlecenie, nie bawimy się w RefreshRates() i uaktualnianie wszystkiego
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOffQuotes e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrRequote e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrOrderLocked e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrLongPositionsOnlyAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTooManyRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeTimeout2 e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout3 e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout4 e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrTradeModifyDenied e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeContextBusy e)
        {
          // trochę dłuższa przerwa; IsTradeContextBusy() musi zwrócić najpierw false
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (mt4.IsTradeContextBusy())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("Trade context busy, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (mt4.IsTradeContextBusy())
          {
            a = -1;
            Logger.Error("Trade context busy, giving up\n");
            break;
          }
        }
        catch (ErrTradeExpirationDenied e)
        {
          // przerwać działanie programu (inna wersja: wyzerować ten parametr i samemu odwoływać niezrealizowane zlecenia po pewnym czasie)
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTooManyOrders e)
        {
          // odpuszczamy od razu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrInvalidTicket e)
        {
          // odpuszczamy od razu; zakładamy, że podaliśmy dobry numer zlecenia, po prostu mogło zostać właśnie zamknięte
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrNoResult e)
        {
          // odpuszczamy od razu; jeśli to nasza wina to poważny błąd, ale może było strasznie duże opóźnienie w przepchnięciu poprzedniej zmiany i nie zaksięgowaliśmy tego więc nie zabijajmy robota
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\nGiving up\n";
          Logger.Error(str, e);
          a = 1; // zlecenie ma żądane parametry więc "sukces"
          break;
        }
        catch (Exception e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderModify():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        if (a > 0) break;
      }
      mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
      return a;
    }

    /// <summary>
    /// Zamyka zlecenie (częściowo lub w całości). Zwraca 1 gdy sukces, -1 gdy porażka.
    /// </summary>
    /// <param name="ticket">Numer zlecenia.</param>
    /// <param name="lots">Jaki wolumen zamknąć.</param>
    /// <param name="price">Cena zamknięcia.</param>
    /// <param name="slippage">Dopuszczalny poślizg.</param>
    /// <returns>1 gdy sukces, -1 gdy porażka, -2 gdy nie wiemy</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    private static int orderClose(int ticket, double lots, double price, int slippage)
    {
      DateTime _creation_time_local_beginning = DateTime.UtcNow;

      int a = -1;
      bool b = false;
      string params_str = "" + InitConfig.Allowed_Symbol + " " + ticket + " " + lots + " " + price + " " + slippage;

      while (true)
      {
        a = -1;
        if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
        {
          break;
        }

        while (!mt4.IsConnected())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("No connection in orderClose(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsConnected())
        {
          a = -1;
          Logger.Error("No connection, giving up\n");
          break;
        }

        while (mt4.IsStopped())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trading stopped in orderClose(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (mt4.IsStopped())
        {
          a = -1;
          Logger.Error("Trading stopped, giving up\n");
          break;
        }

        // NB: gdyby było kilka EA działających jednocześnie, trzeba by używać semaforów (np. w postaci zmiennych globalnych wewnątrz MT4)
        while (!mt4.IsTradeAllowed())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trade not allowed in orderClose(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsTradeAllowed())
        {
          a = -1;
          Logger.Error("Trade not allowed, giving up\n");
          break;
        }

        try
        {
          mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
          Console.WriteLine("Making request OrderClose() with params: " + params_str);
          // TODO: może jakieś kolorki przy zamykaniu (np. zachować poprzednie albo coś nowego dać)
          _sw.Restart();
          b = mt4.OrderClose(ticket, lots, price, slippage, new Color());
          if (b)
          {
            a = 1;
            Console.WriteLine("OrderClose() made successfull request (in " + _sw.ElapsedMilliseconds + "ms) with params: " + params_str);
          }
          else
          {
            // pytanie czy w ogóle tu kiedykolwiek wejdziemy....
            a = -1;
            int err_no = mt4.GetLastError();
            string str = "OrderClose() failed (in " + _sw.ElapsedMilliseconds + "ms), error " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
            Logger.Error(str);
            System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
            // pewnie będzie powtórka bo nie wychodzimy z pętli
            // na przykład dla ERR_BROKER_BUSY (137) wypada powtórzyć, ale podobno ten błąd się nie pojawia
            // dla innych mam nadzieję, że raczej będzie rzucony wyjątek bo czasem nie ma sensu powtarzać
          }
        }
        catch (ErrInvalidFunctionParamvalue e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrCustomIndicatorError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrIntegerParameterExpected e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrUnknownSymbol e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidPriceParam e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeNotAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrCommonError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidTradeParameters e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrServerBusy e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOldVersion e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrNoConnection e)
        {
          // trochę dłuższa przerwa; może użyć IsConnected()
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (!mt4.IsConnected())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("No connection, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (!mt4.IsConnected())
          {
            a = -1;
            Logger.Error("No connection, giving up\n");
            break;
          }
        }
        catch (ErrTooFrequentRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrAccountDisabled e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidAccount e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTimeout e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrInvalidPrice e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidStops e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidTradeVolume e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrMarketClosed e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeDisabled e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrNotEnoughMoney e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrPriceChanged e)
        {
          // trochę dłuższa przerwa, może uda się wpakować zlecenie, nie bawimy się w RefreshRates() i uaktualnianie wszystkiego
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOffQuotes e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrRequote e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrOrderLocked e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrLongPositionsOnlyAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTooManyRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeTimeout2 e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout3 e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout4 e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrTradeModifyDenied e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeContextBusy e)
        {
          // trochę dłuższa przerwa; IsTradeContextBusy() musi zwrócić najpierw false
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (mt4.IsTradeContextBusy())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("Trade context busy, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (mt4.IsTradeContextBusy())
          {
            a = -1;
            Logger.Error("Trade context busy, giving up\n");
            break;
          }
        }
        catch (ErrTradeExpirationDenied e)
        {
          // przerwać działanie programu (inna wersja: wyzerować ten parametr i samemu odwoływać niezrealizowane zlecenia po pewnym czasie)
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTooManyOrders e)
        {
          // odpuszczamy od razu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrInvalidTicket e)
        {
          // odpuszczamy od razu; zakładamy, że podaliśmy dobry numer zlecenia, po prostu mogło zostać właśnie zamknięte
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (Exception e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderClose():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        if (a > 0) break;
      }
      mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
      return a;
    }

    /// <summary>
    /// Zamyka zlecenie (częściowo lub w całości) przy użyciu zlecenia przeciwnego. Zwraca 1 gdy sukces, -1 gdy porażka.
    /// </summary>
    /// <param name="ticket">Numer zlecenia zamykanego.</param>
    /// <param name="opposite">Numer zlecenia przeciwnego.</param>
    /// <returns>1 gdy sukces, -1 gdy porażka, -2 gdy nie znamy rezultatu</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    private static int orderCloseBy(int ticket, int opposite)
    {
      DateTime _creation_time_local_beginning = DateTime.UtcNow;

      int a = -1;
      bool b = false;
      string params_str = "" + InitConfig.Allowed_Symbol + " " + ticket + " " + opposite;

      while (true)
      {
        a = -1;
        if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
        {
          break;
        }

        while (!mt4.IsConnected())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("No connection in orderCloseBy(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsConnected())
        {
          a = -1;
          Logger.Error("No connection, giving up\n");
          break;
        }

        while (mt4.IsStopped())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trading stopped in orderCloseBy(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (mt4.IsStopped())
        {
          a = -1;
          Logger.Error("Trading stopped, giving up\n");
          break;
        }

        // NB: gdyby było kilka EA działających jednocześnie, trzeba by używać semaforów (np. w postaci zmiennych globalnych wewnątrz MT4)
        while (!mt4.IsTradeAllowed())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trade not allowed in orderCloseBy(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsTradeAllowed())
        {
          a = -1;
          Logger.Error("Trade not allowed, giving up\n");
          break;
        }

        try
        {
          mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
          Console.WriteLine("Making request OrderCloseBy() with params: " + params_str);
          // TODO: może jakieś kolorki przy zamykaniu (np. zachować poprzednie albo coś nowego dać)
          _sw.Restart();
          b = mt4.OrderCloseBy(ticket, opposite, new Color());
          if (b)
          {
            a = 1;
            Console.WriteLine("OrderCloseBy() made successfull request (in " + _sw.ElapsedMilliseconds + "ms) with params: " + params_str);
          }
          else
          {
            // pytanie czy w ogóle tu kiedykolwiek wejdziemy....
            a = -1;
            int err_no = mt4.GetLastError();
            string str = "OrderCloseBy() failed (in " + _sw.ElapsedMilliseconds + "ms), error " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
            Logger.Error(str);
            System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
            // pewnie będzie powtórka bo nie wychodzimy z pętli
            // na przykład dla ERR_BROKER_BUSY (137) wypada powtórzyć, ale podobno ten błąd się nie pojawia
            // dla innych mam nadzieję, że raczej będzie rzucony wyjątek bo czasem nie ma sensu powtarzać
          }
        }
        catch (ErrInvalidFunctionParamvalue e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrCustomIndicatorError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrIntegerParameterExpected e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrUnknownSymbol e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeNotAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrCommonError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidTradeParameters e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrServerBusy e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOldVersion e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrNoConnection e)
        {
          // trochę dłuższa przerwa; może użyć IsConnected()
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (!mt4.IsConnected())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("No connection, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (!mt4.IsConnected())
          {
            a = -1;
            Logger.Error("No connection, giving up\n");
            break;
          }
        }
        catch (ErrTooFrequentRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrAccountDisabled e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidAccount e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTimeout e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrInvalidPrice e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidStops e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidTradeVolume e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrMarketClosed e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeDisabled e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrNotEnoughMoney e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrPriceChanged e)
        {
          // trochę dłuższa przerwa, może uda się wpakować zlecenie, nie bawimy się w RefreshRates() i uaktualnianie wszystkiego
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOffQuotes e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrRequote e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrOrderLocked e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrLongPositionsOnlyAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTooManyRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeTimeout2 e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout3 e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout4 e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrTradeModifyDenied e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeContextBusy e)
        {
          // trochę dłuższa przerwa; IsTradeContextBusy() musi zwrócić najpierw false
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (mt4.IsTradeContextBusy())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("Trade context busy, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (mt4.IsTradeContextBusy())
          {
            a = -1;
            Logger.Error("Trade context busy, giving up\n");
            break;
          }
        }
        catch (ErrTradeExpirationDenied e)
        {
          // przerwać działanie programu (inna wersja: wyzerować ten parametr i samemu odwoływać niezrealizowane zlecenia po pewnym czasie)
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTooManyOrders e)
        {
          // odpuszczamy od razu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrInvalidTicket e)
        {
          // odpuszczamy od razu; zakładamy, że podaliśmy dobry numer zlecenia, po prostu mogło zostać właśnie zamknięte
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (Exception e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderCloseBy():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        if (a > 0) break;
      }
      mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
      return a;
    }

    /// <summary>
    /// Kasuje oczekujące zlecenie. Zwraca 1 gdy sukces, -1 gdy porażka.
    /// </summary>
    /// <param name="ticket">Numer zlecenia zamykanego.</param>
    /// <param name="opposite">Numer zlecenia komplementarnego.</param>
    /// <returns>1 gdy sukces, -1 gdy porażka, -2 gdy nie wiemy</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    private static int orderDelete(int ticket)
    {
      DateTime _creation_time_local_beginning = DateTime.UtcNow;

      int a = -1;
      bool b = false;
      string params_str = "" + InitConfig.Allowed_Symbol + " " + ticket;

      while (true)
      {
        a = -1;
        if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
        {
          break;
        }

        while (!mt4.IsConnected())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("No connection in orderDelete(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsConnected())
        {
          a = -1;
          Logger.Error("No connection, giving up\n");
          break;
        }

        while (mt4.IsStopped())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trading stopped in orderDelete(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (mt4.IsStopped())
        {
          a = -1;
          Logger.Error("Trading stopped, giving up\n");
          break;
        }

        // NB: gdyby było kilka EA działających jednocześnie, trzeba by używać semaforów (np. w postaci zmiennych globalnych wewnątrz MT4)
        while (!mt4.IsTradeAllowed())
        {
          if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
          {
            break;
          }
          Logger.Error("Trade not allowed in orderDelete(), will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
          System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
        }
        if (!mt4.IsTradeAllowed())
        {
          a = -1;
          Logger.Error("Trade not allowed, giving up\n");
          break;
        }

        try
        {
          mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
          Console.WriteLine("Making request OrderDelete() with params: " + params_str);
          // TODO: może jakieś kolorki przy zamykaniu (np. zachować poprzednie albo coś nowego dać)
          _sw.Restart();
          b = mt4.OrderDelete(ticket, new Color());
          if (b)
          {
            a = 1;
            Console.WriteLine("OrderDelete() made successfull request (in " + _sw.ElapsedMilliseconds + "ms) with params: " + params_str);
          }
          else
          {
            // pytanie czy w ogóle tu kiedykolwiek wejdziemy....
            a = -1;
            int err_no = mt4.GetLastError();
            string str = "OrderDelete() failed (in " + _sw.ElapsedMilliseconds + "ms), error " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
            Logger.Error(str);
            System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
            // pewnie będzie powtórka bo nie wychodzimy z pętli
            // na przykład dla ERR_BROKER_BUSY (137) wypada powtórzyć, ale podobno ten błąd się nie pojawia
            // dla innych mam nadzieję, że raczej będzie rzucony wyjątek bo czasem nie ma sensu powtarzać
          }
        }
        catch (ErrInvalidFunctionParamvalue e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrCustomIndicatorError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrUnknownSymbol e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeNotAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrCommonError e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidTradeParameters e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrServerBusy e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOldVersion e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrNoConnection e)
        {
          // trochę dłuższa przerwa; może użyć IsConnected()
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (!mt4.IsConnected())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("No connection, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (!mt4.IsConnected())
          {
            a = -1;
            Logger.Error("No connection, giving up\n");
            break;
          }
        }
        catch (ErrTooFrequentRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrAccountDisabled e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrInvalidAccount e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTimeout e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrInvalidPrice e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidStops e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrInvalidTradeVolume e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrMarketClosed e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeDisabled e)
        {
          // dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrNotEnoughMoney e)
        {
          // przerwać działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrPriceChanged e)
        {
          // trochę dłuższa przerwa, może uda się wpakować zlecenie, nie bawimy się w RefreshRates() i uaktualnianie wszystkiego
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrOffQuotes e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrRequote e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrOrderLocked e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrLongPositionsOnlyAllowed e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTooManyRequests e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeTimeout2 e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout3 e)
        {
          // odpuszczamy próby od razu, nie wiemy czy udało się
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          a = -2;
          break;
        }
        catch (ErrTradeTimeout4 e)
        {
          // odpuszczamy próby od razu, nie udało się; spróbujemy (o ile warunki będą ok) przy następnym ticku
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrTradeModifyDenied e)
        {
          // trochę dłuższa przerwa
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
        }
        catch (ErrTradeContextBusy e)
        {
          // trochę dłuższa przerwa; IsTradeContextBusy() musi zwrócić najpierw false
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nWill wait " + LocalConfig.ORDER_BIG_DELAY + "ms\n";
          Logger.Error(str, e);
          System.Threading.Thread.Sleep(LocalConfig.ORDER_BIG_DELAY);
          while (mt4.IsTradeContextBusy())
          {
            if (DateTime.UtcNow - _creation_time_local_beginning > LocalConfig.ORDER_SUM_DELAY_TIMESPAN)
            {
              break;
            }
            Logger.Error("Trade context busy, will wait " + LocalConfig.ORDER_SMALL_DELAY + "ms\n");
            System.Threading.Thread.Sleep(LocalConfig.ORDER_SMALL_DELAY);
          }
          if (mt4.IsTradeContextBusy())
          {
            a = -1;
            Logger.Error("Trade context busy, giving up\n");
            break;
          }
        }
        catch (ErrTradeExpirationDenied e)
        {
          // przerwać działanie programu (inna wersja: wyzerować ten parametr i samemu odwoływać niezrealizowane zlecenia po pewnym czasie)
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n" + e.description() + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        catch (ErrTradeTooManyOrders e)
        {
          // odpuszczamy od razu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (ErrInvalidTicket e)
        {
          // odpuszczamy od razu; zakładamy, że podaliśmy dobry numer zlecenia, po prostu mogło zostać właśnie zamknięte
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\nGiving up\n";
          Logger.Error(str, e);
          a = -1;
          break;
        }
        catch (Exception e)
        {
          // przerywamy działanie programu
          string str = "Caught exception (in " + _sw.ElapsedMilliseconds + "ms) in orderDelete():\n\tparams: " + params_str + "\n";
          int err_no = mt4.GetLastError();
          str += "\terror " + err_no + ": " + Lib.ErrorDescription(err_no) + "\n";
          Logger.Fatal(str, e);
          throw;
        }
        if (a > 0) break;
      }
      mt4.GetLastError(); // czyścimy zmienną globalną przechowującą numer błędu
      return a;
    }

    /// <summary>
    /// Kasuj zlecenie (wywoływać tylko dla Pending!). Zwraca True gdy się udało i False wpp (także gdy rezultat nieznany).
    /// </summary>
    /// <returns>Zwraca True gdy się udało i False wpp.</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    public bool delete()
    {
      int ret = -1;
      if (status == Enums.PositionStatus.Pending)
      {
        if (canBeDeleted())
        {
          ret = Position.orderDelete(id);
          if (ret == 1)
          {
            //status = Enums.PositionStatus.Deleted;
            // róbmy to jednak przez update() po księgowaniu zmian
          }
          else Logger.Error("delete(): can't delete order " + id);
        }
        else
        {
          Logger.Error("delete(): can't delete order " + id + " (FREEZELEVEL " + E.FREEZELEVEL + " open_price " + wanted_opening_price + ")");
        }
      }
      else
      {
        Logger.Error("delete(): can't delete order " + id + " (it has type " + status + ")");
      }

      if (ret < 0) return false;
      else
      {
        last_modification_date = E.TIME;
        return true;
      }
    }

    /// <summary>
    /// Czy można zamknąć pozycję (nie ma naruszenia FREEZELEVEL).
    /// </summary>
    /// <param name="x">Pozycja.</param>
    /// <returns>Czy można zamknąć pozycję.</returns>
    private static bool canBeClosed(Position x)
    {
      bool ret = false;
      if (x.status == Enums.PositionStatus.Opened)
      {
        ret = true;
        if (x.dir == Enums.LimitedDirection.Up)
        {
          if (Lib.comp(E.BID - (decimal)x.SL, E.FREEZELEVEL * E.POINT) <= 0 && Lib.comp((decimal)x.TP - E.BID, E.FREEZELEVEL * E.POINT) <= 0) ret = false;
        }
        else
        {
          if (Lib.comp((decimal)x.SL - E.ASK, E.FREEZELEVEL * E.POINT) <= 0 && Lib.comp(E.ASK - (decimal)x.TP, E.FREEZELEVEL * E.POINT) <= 0) ret = false;
        }
      }
      return ret;
    }

    /// <summary>
    /// Czy można zamknąć pozycję (nie ma naruszenia FREEZELEVEL).
    /// </summary>
    /// <returns>Czy można zamknąć pozycję.</returns>
    public bool canBeClosed()
    {
      return Position.canBeClosed(this);
    }

    /// <summary>
    /// Czy można usunąć lub zmodyfikować pozycję oczkekującą (nie ma naruszenia FREEZELEVEL).
    /// </summary>
    /// <param name="x">Pozycja.</param>
    /// <returns>Czy można usunąć pozycję.</returns>
    private static bool canBeDeleted(Position x)
    {
      bool ret = false;
      if (x.status == Enums.PositionStatus.Pending)
      {
        ret = true;
        switch (x.operation)
        {
          case TradeOperation.OP_BUYLIMIT:
            if (Lib.comp(E.ASK - (decimal)x.wanted_opening_price, E.FREEZELEVEL * E.POINT) <= 0) ret = false;
            break;
          case TradeOperation.OP_SELLLIMIT:
            if (Lib.comp((decimal)x.wanted_opening_price - E.BID, E.FREEZELEVEL * E.POINT) <= 0) ret = false;
            break;
          case TradeOperation.OP_BUYSTOP:
            if (Lib.comp((decimal)x.wanted_opening_price - E.ASK, E.FREEZELEVEL * E.POINT) <= 0) ret = false;
            break;
          case TradeOperation.OP_SELLSTOP:
            if (Lib.comp(E.BID - (decimal)x.wanted_opening_price, E.FREEZELEVEL * E.POINT) <= 0) ret = false;
            break;
        }
      }
      return ret;
    }

    /// <summary>
    /// Czy można usunąć lub zmodyfikować pozycję oczkekującą  (nie ma naruszenia FREEZELEVEL).
    /// </summary>
    /// <returns>Czy można usunąć pozycję.</returns>
    public bool canBeDeleted()
    {
      return Position.canBeDeleted(this);
    }

    /// <summary>
    /// Zamknij zlecenie (wywoływać tylko dla Opened!). Zwraca True gdy się udało i False wpp (także gdy rezultat nieznany).
    /// </summary>
    /// <param name="_slippage">Jaki poślizg podczas zamknięcia akceptujemy.</param>
    /// <param name="_lots">Rozmiar do zamknięcia (gdy domyślne -1 - zamknij całość).</param>
    /// <returns>Zwraca True gdy się udało i False wpp.</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    public bool close(decimal _slippage, decimal _lots = -1)
    {
      int ret = -1;
      if (status == Enums.PositionStatus.Opened)
      {
        if (canBeClosed())
        {
          decimal _price = (dir == Enums.LimitedDirection.Up ? E.BID : E.ASK);
          double price = normalize(_price);
          double lots = (_lots < 0 ? volume : normalize(_lots, E.LOTSTEP));
          if (lots < (double)E.MINLOT || lots > volume)
          {
            Logger.Error("close(): lots " + lots + " (made of " + _lots + ") out of bounds: " + ((double)E.MINLOT) + " " + volume);
            return false;
          }
          int slippage = half_normalize(_slippage);
          ret = Position.orderClose(id, lots, price, slippage);
          if (ret == 1)
          {
            //status = Enums.PositionStatus.Closed;
            // róbmy to jednak przez update() po księgowaniu zmian
            allowed_closing_slippage = slippage;
            wanted_closing_price = price;
          }
          else Logger.Error("close(): can't close order " + id);
        }
        else
        {
          Logger.Error("close(): can't close order " + id + " (FREEZELEVEL " + E.FREEZELEVEL + " SL " + SL + " TP " + TP + ")");
        }
      }
      else
      {
        Logger.Error("close(): can't close order " + id + " (it has type " + status + ")");
      }

      if (ret < 0) return false;
      else
      {
        last_modification_date = E.TIME;
        return true;
      }
    }

    /// <summary>
    /// Zamknij zlecenie (wywoływać tylko dla Opened!) przy pomocy przeciwnego. Zwraca True gdy się udało i False wpp (także gdy rezultat nieznany).
    /// </summary>
    /// <param name="complementary">Zlecenie przeciwne.</param>
    /// <returns>Zwraca True gdy się udało i False wpp.</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    public bool close(Position complementary)
    {
      if (status != Enums.PositionStatus.Opened)
      {
        Logger.Error("close(Position): can't close order " + id + " (it has type " + status + ")");
        return false;
      }
      if (complementary.status != Enums.PositionStatus.Opened)
      {
        Logger.Error("close(Position): can't close complementary order " + complementary.id + " (it has type " + complementary.status + ")");
        return false;
      }
      if (complementary.dir == dir)
      {
        Logger.Error("close(Position): can't close order " + id + " and " + complementary.id + " (both have direction " + dir + ")");
        return false;
      }
      if (!canBeClosed())
      {
        Logger.Error("close(Position): can't close order " + id + " (FREEZELEVEL " + E.FREEZELEVEL + " SL " + SL + " TP " + TP + ")");
        return false;
      }
      if (!complementary.canBeClosed())
      {
        Logger.Error("close(Position): can't close complementary order " + complementary.id + " (FREEZELEVEL " + E.FREEZELEVEL + " SL " + complementary.SL + " TP " + complementary.TP + ")");
        return false;
      }
      int ret = Position.orderCloseBy(id, complementary.id);
      if (ret == 1 || ret == -2)
      {
        //status = complementary.status = Enums.PositionStatus.Closed;
        // róbmy to jednak przez update() po księgowaniu zmian
        // linijka poniżej odpala potem warninga podczas testów offline bo E.SPREAD bywa mniejszy niż E.spread/E.POINT (dla danych 4digits i punktu 5digits)
        //allowed_closing_slippage = complementary.allowed_closing_slippage = (int)E.SPREAD;
        // więc trochę naokoło:
        allowed_closing_slippage = complementary.allowed_closing_slippage = half_normalize(E.spread);
        closed_by = complementary.id;
        complementary.closed_by = this.id;
        if (dir == Enums.LimitedDirection.Up)
        {
          wanted_closing_price = normalize(E.BID);
          complementary.wanted_closing_price = normalize(E.ASK);
        }
        else
        {
          wanted_closing_price = normalize(E.ASK);
          complementary.wanted_closing_price = normalize(E.BID);
        }
        if (ret == -2)
        {
          Logger.Error("close(Position): timeout when closing order " + id + " and " + complementary.id);
        }
      }
      else
      {
        Logger.Error("close(Position): can't close order " + id + " and " + complementary.id);
      }

      if (ret < 0) return false;
      else
      {
        last_modification_date = E.TIME;
        return true;
      }
    }

    /// <summary>
    /// Modyfikuj zlecenie (wywoływać tylko dla Opened lub Pending!). Zwraca True gdy się udało i False wpp (także gdy rezultat nieznany).
    /// </summary>
    /// <param name="_stoploss">Nowy poziom S/L (domyślnie jest -1 - gdy chcemy użyć starego).</param>
    /// <param name="_takeprofit">Nowy poziom T/P (domyślnie jest -1 - gdy chcemy użyć starego).</param>
    /// <param name="_opening_price">Nowa żądana cena otwarcia O/P (domyślnie jest -1 - gdy chcemy użyć starej). Ma znaczenie tylko dla zleceń Pending.</param>
    /// <param name="_valid_in_minutes">Jak długo zlecenie ma być ważne, w minutach (domyślnie jest -1 - gdy chcemy użyć starej daty wygasania). Ma znaczenie tylko dla zleceń Pending.</param>
    /// <returns>Zwraca True gdy się udało i False wpp.</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    public bool modify(decimal _stoploss = -1, decimal _takeprofit = -1, decimal _opening_price = -1, int _valid_in_minutes = -1)
    {
      double price = normalize(_opening_price);
      double stoploss = normalize(_stoploss);
      double takeprofit = normalize(_takeprofit);
      DateTime expiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

      double OP_lim = 0, SL_lim = 0, TP_lim = 0, SL_lim2 = 0, TP_lim2 = 0;
      if (status == Enums.PositionStatus.Opened)
      {
        price = opening_price;
        if (_takeprofit < 0)
        {
          takeprofit = TP;
          // nie obcinajmy odległego TP jeśli nie modyfikujemy starego TP
          TP_lim2 = TP;
        }
        else
        {
          TP_lim2 = normalize(getTP_lim2());
        }
        if (_stoploss < 0)
        {
          stoploss = SL;
        }
        if (Lib.comp(takeprofit, TP) == 0 && Lib.comp(stoploss, SL) == 0)
        {
          Logger.Error("modify(): " + id + " S/L " + stoploss + " and T/P " + takeprofit + " haven't changed so won't modify an order");
          return false;
        }

        SL_lim = normalize(getSL_lim());
        TP_lim = normalize(getTP_lim());
        SL_lim2 = normalize(getSL_lim2());

        if (dir == Enums.LimitedDirection.Up)
        {
          if (Lib.comp(stoploss, SL) < 0)
          {
            Logger.Error("modify(): " + id + " can't loosen S/L from " + SL + " to " + stoploss + " so won't modify an order");
            return false;
          }
          if (Lib.comp(stoploss, SL_lim) > 0)
          {
            Logger.Error("modify(): " + id + " wanted S/L " + stoploss + " is greater than border " + SL_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(takeprofit, TP_lim) < 0)
          {
            Logger.Error("modify(): " + id + " wanted T/P " + takeprofit + " is lesser than border " + TP_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(stoploss, SL_lim2) < 0)
          {
            Logger.Error("modify(): " + id + " wanted S/L " + stoploss + " is lesser than border " + SL_lim2 + " so won't modify an order");
            return false;
          }
          if (Lib.comp(takeprofit, TP_lim2) > 0)
          {
            Logger.Error("modify(): " + id + " wanted T/P " + takeprofit + " is greater than border " + TP_lim2 + " so won't modify an order");
            return false;
          }
        }
        else
        {
          if (Lib.comp(stoploss, SL) > 0)
          {
            Logger.Error("modify(): " + id + " can't loosen S/L from " + SL + " to " + stoploss + " so won't modify an order");
            return false;
          }
          if (Lib.comp(stoploss, SL_lim) < 0)
          {
            Logger.Error("modify(): " + id + " wanted S/L " + stoploss + " is lesser than border " + SL_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(takeprofit, TP_lim) > 0)
          {
            Logger.Error("modify(): " + id + " wanted T/P " + takeprofit + " is greater than border " + TP_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(stoploss, SL_lim2) > 0)
          {
            Logger.Error("modify(): " + id + " wanted S/L " + stoploss + " is greater than border " + SL_lim2 + " so won't modify an order");
            return false;
          }
          if (Lib.comp(takeprofit, TP_lim2) < 0)
          {
            Logger.Error("modify(): " + id + " wanted T/P " + takeprofit + " is lesser than border " + TP_lim2 + " so won't modify an order");
            return false;
          }
        }
      }
      else if (status == Enums.PositionStatus.Pending)
      {
        if (!canBeDeleted())
        {
          Logger.Error("modify(): " + id + " (FREEZELEVEL " + E.FREEZELEVEL + " open_price " + wanted_opening_price + " so won't modify an order");
        }
        if (_valid_in_minutes < 0)
        {
          expiration = opening_expiration;
        }
        else
        {
          expiration = E.TIME + new TimeSpan(0, _valid_in_minutes, 0);
          expiration -= new TimeSpan(0, 0, 0, expiration.Second, expiration.Millisecond);
        }
        if (_opening_price < 0)
        {
          price = wanted_opening_price;
        }
        if (_takeprofit < 0)
        {
          takeprofit = wanted_opening_TP;
        }
        if (_stoploss < 0)
        {
          stoploss = wanted_opening_SL;
        }
        if (Lib.comp(takeprofit, wanted_opening_TP) == 0 && Lib.comp(stoploss, wanted_opening_SL) == 0 && Lib.comp(price, wanted_opening_price) == 0 && expiration == opening_expiration)
        {
          Logger.Error("modify(): " + id + " S/L " + stoploss + " T/P " + takeprofit + " O/P " + price + " and expiration " + expiration + " haven't changed so won't modify an order");
          return false;
        }

        OP_lim = normalize(getOP_lim());
        double OP_min = normalize(getOP_min());
        SL_lim = normalize(getSL_lim((decimal)price));
        TP_lim = normalize(getTP_lim((decimal)price));
        SL_lim2 = normalize(getSL_lim2((decimal)price));
        TP_lim2 = normalize(getTP_lim2((decimal)price));
        if (dir == Enums.LimitedDirection.Up)
        {
          if (Lib.comp(stoploss, SL_lim) > 0)
          {
            Logger.Error("modify(): " + id + " wanted S/L " + stoploss + " is greater than border " + SL_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(takeprofit, TP_lim) < 0)
          {
            Logger.Error("modify(): " + id + " wanted T/P " + takeprofit + " is lesser than border " + TP_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(stoploss, SL_lim2) < 0)
          {
            Logger.Error("modify(): " + id + " wanted S/L " + stoploss + " is lesser than border " + SL_lim2 + " so won't modify an order");
            return false;
          }
          if (Lib.comp(takeprofit, TP_lim2) > 0)
          {
            Logger.Error("modify(): " + id + " wanted T/P " + takeprofit + " is greater than border " + TP_lim2 + " so won't modify an order");
            return false;
          }
        }
        else
        {
          if (Lib.comp(stoploss, SL_lim) < 0)
          {
            Logger.Error("modify(): " + id + " wanted S/L " + stoploss + " is lesser than border " + SL_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(takeprofit, TP_lim) > 0)
          {
            Logger.Error("modify(): " + id + " wanted T/P " + takeprofit + " is greater than border " + TP_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(stoploss, SL_lim2) > 0)
          {
            Logger.Error("modify(): " + id + " wanted S/L " + stoploss + " is greater than border " + SL_lim2 + " so won't modify an order");
            return false;
          }
          if (Lib.comp(takeprofit, TP_lim2) < 0)
          {
            Logger.Error("modify(): " + id + " wanted T/P " + takeprofit + " is lesser than border " + TP_lim2 + " so won't modify an order");
            return false;
          }
        }
        if (operation == TradeOperation.OP_BUYSTOP || operation == TradeOperation.OP_SELLLIMIT)
        {
          if (Lib.comp(price, OP_lim) < 0)
          {
            Logger.Error("modify(): " + id + " wanted O/P " + price + " is lesser than border " + OP_lim + " so won't modify an order");
            return false;
          }
        }
        else
        {
          if (Lib.comp(price, OP_lim) > 0)
          {
            Logger.Error("modify(): " + id + " wanted O/P " + price + " is greater than border " + OP_lim + " so won't modify an order");
            return false;
          }
          if (Lib.comp(price, OP_min) < 0)
          {
            Logger.Error("modify(): " + id + " wanted O/P " + price + " is lesser than border " + OP_min + " so won't modify an order");
            return false;
          }
        }
      }
      else
      {
        Logger.Error("modify(): " + id + " can't be modified due to its type " + status);
        return false;
      }

      int ret = Position.orderModify(id, price, stoploss, takeprofit, expiration);
      if (ret == 1)
      {
        if (status == Enums.PositionStatus.Opened)
        {
          SL = stoploss;
          TP = takeprofit;
          params_changes.Add(Tuple.Create(E.tick_counter, E.TIME, stoploss, takeprofit));
        }
        else if (status == Enums.PositionStatus.Pending)
        {
          wanted_opening_price = price;
          SL = wanted_opening_SL = stoploss;
          TP = wanted_opening_TP = takeprofit;
          opening_expiration = expiration;
        }
      }
      else
      {
        Logger.Error("modify(): can't modify order " + id);
      }

      if (ret < 0) return false;
      else
      {
        last_modification_date = E.TIME;
        return true;
      }
    }

    /// <summary>
    /// Zaadoptuj pozycję z MT4. Po restarcie mogą być na platformie pozycje, które robot powinien obsługiwac więc musi je najpierw zaadoptować.
    /// </summary>
    /// <param name="_tactics">Taktyka zarządzania pozycją.</param>
    /// <param name="_OrderType">Rodzaj zlecenia.</param>
    /// <param name="_OrderTicket">Numer zlecenia.</param>
    /// <param name="_OrderClosePrice">Cena zamknięcia.</param>
    /// <param name="_OrderCloseTime">Czas zamknięcia.</param>
    /// <param name="_OrderComment">Komentarz opisujący zlecenie.</param>
    /// <param name="_OrderCommission">Prowizja.</param>
    /// <param name="_OrderExpiration">Data wygaśnięcia zlecenia.</param>
    /// <param name="_OrderLots">Wolumen w lotach.</param>
    /// <param name="_OrderOpenPrice">Cena otwarcia.</param>
    /// <param name="_OrderOpenTime">Czas otwarcia.</param>
    /// <param name="_OrderProfit">Zysk/strata na zleceniu.</param>
    /// <param name="_OrderStopLoss">Poziom S/L.</param>
    /// <param name="_OrderSwap">Koszty swapowania.</param>
    /// <param name="_OrderSymbol">Para walutowa.</param>
    /// <param name="_OrderTakeProfit">Poziom T/P.</param>
    /// <returns>Obiekt pozycji.</returns>
    public static Position adopt(Enums.PositionTactics _tactics, TradeOperation _OrderType, int _OrderTicket, double _OrderClosePrice, DateTime _OrderCloseTime, string _OrderComment, double _OrderCommission, DateTime _OrderExpiration, double _OrderLots, double _OrderOpenPrice, DateTime _OrderOpenTime, double _OrderProfit, double _OrderStopLoss, double _OrderSwap, string _OrderSymbol, double _OrderTakeProfit)
    {
      Enums.PositionStatus status = Enums.PositionStatus.Pending;
      if (_OrderType == TradeOperation.OP_BUY || _OrderType == TradeOperation.OP_SELL)
      {
        status = Enums.PositionStatus.Opened;
      }

      Enums.LimitedDirection _dir = Enums.LimitedDirection.Up;
      if (_OrderType == TradeOperation.OP_SELL || _OrderType == TradeOperation.OP_SELLSTOP || _OrderType == TradeOperation.OP_SELLLIMIT) _dir = Enums.LimitedDirection.Down;

      Enums.PositionProfitStatus profit_status = Enums.PositionProfitStatus.Losing; // najbliższy update powinien ustawić prawdziwą wartość
      Enums.PositionProfitStatus profit_status_volatile = Enums.PositionProfitStatus.Losing; // najbliższy update powinien ustawić prawdziwą wartość

      Enums.MagicNumbers magic_num = Enums.MagicNumbers.Aggressive;
      if (_tactics == Enums.PositionTactics.Peak) magic_num = Enums.MagicNumbers.Peak;
      else if (_tactics == Enums.PositionTactics.Strict) magic_num = Enums.MagicNumbers.Strict;

      Position p = new Position(_OrderTicket, E.TIME, E.tick_counter, _dir, _OrderType, _OrderLots, _OrderOpenPrice, 0, _OrderStopLoss, _OrderTakeProfit, _OrderExpiration, _tactics, _OrderOpenPrice, _OrderOpenTime, _OrderStopLoss, _OrderTakeProfit, -1, _OrderClosePrice, _OrderCloseTime, _OrderCommission, _OrderSwap, _OrderProfit, status, profit_status, profit_status_volatile, magic_num, _OrderComment);
      return p;
    }

    /// <summary>
    /// Złóż nowe zlecenie. Zwraca obiekt pozycji gdy się udało (status==Unknown i id==-1 gdy nie wiadomo czy się udało na pewno) i null w przypadku porażki.
    /// </summary>
    /// <param name="_tactics">Taktyka zarządzania tą pozycją.</param>
    /// <param name="cmd">Rodzaj zlecenia.</param>
    /// <param name="_volume">Rozmiar pozycji.</param>
    /// <param name="_stoploss">Poziom S/L.</param>
    /// <param name="_takeprofit">Poziom T/P (domyślnie -1 - wtedy ustawiamy absurdalnie odległy T/P).</param>
    /// <param name="_price">Żądana cena otwarcia (ważne tylko dla zleceń pending).</param>
    /// <param name="_slippage">Dozwolony poślizg przy otwieraniu zleceń po cenie rynkowej (domyślnie 0, ale to zbyt mała wartość raczej).</param>
    /// <param name="comment">Komentarz opisujący zlecenie.</param>
    /// <param name="_valid_in_minutes">Jak długo ma być ważne zlecenie oczekujące (ile minut) - domyślna wartość to bezterminowo.</param>
    /// <returns>Zwraca obiekt pozycji gdy się udało (status==Unknown i id==-1 gdy nie wiadomo czy się udało na pewno) i null w przypadku porażki.</returns>
    /// <exception cref="Exception">Dowolny wyjątek - konieczność zakończenia programu.</exception>
    public static Position open(Enums.PositionTactics _tactics, TradeOperation cmd, decimal _volume, decimal _stoploss, decimal _takeprofit = -1, decimal _price = -1, decimal _slippage = 0, string comment = "", int _valid_in_minutes = -1)
    {
      /* Jeszcze taka uwaga:
       * http://www.wildbunny.co.uk/blog/2013/04/17/algorithmic-trading-for-dummies-part-2/
       * There is also a further restriction on all pending orders, that they may not be placed ‘inside’ the current spread. So even if your broker has a stop-level of 0, you still cannot place any pending orders (including take-profit and stop-loss) anywhere between the Ask and Bid prices.
       * Ale nie ma tego w dokumentacji MQL4 więc nie uwzględniamy
       */

      if (_slippage < 0)
      {
        Logger.Error("open(): slippage can't be negative");
        return null;
      }
      decimal _price_compl = _price;
      Enums.PositionStatus status = Enums.PositionStatus.Pending;
      if (cmd == TradeOperation.OP_BUY)
      {
        _price = E.ASK;
        _price_compl = E.BID;
        status = Enums.PositionStatus.Opened;
      }
      else if (cmd == TradeOperation.OP_SELL)
      {
        _price = E.BID;
        _price_compl = E.ASK;
        status = Enums.PositionStatus.Opened;
      }
      else
      {
        _slippage = 0;
      }
      bool m_TP = false;
      if (cmd == TradeOperation.OP_BUY || cmd == TradeOperation.OP_BUYLIMIT || cmd == TradeOperation.OP_BUYSTOP)
      {
        if (Lib.comp(_stoploss, _price_compl * TAGeneralConfig.MAX_STOPLOSS_LIMIT) < 0)
        {
          Logger.Error("open(): S/L " + _stoploss + " lower than " + (_price_compl * TAGeneralConfig.MAX_STOPLOSS_LIMIT));
          return null;
        }
        if (_takeprofit < 0)
        {
          _takeprofit = _price_compl * TAGeneralConfig.MAX_TAKEPROFIT_LIMIT;
          m_TP = true;
        }
      }
      else
      {
        if (Lib.comp(_stoploss, _price_compl * (2 - TAGeneralConfig.MAX_STOPLOSS_LIMIT)) > 0)
        {
          Logger.Error("open(): S/L " + _stoploss + " higher than " + (_price_compl * (1 + TAGeneralConfig.MAX_STOPLOSS_LIMIT)));
          return null;
        }
        if (_takeprofit < 0)
        {
          _takeprofit = E.TICKSIZE;
          m_TP = true;
        }
      }
      double volume = normalize(_volume, E.LOTSTEP);
      if (volume < (double)E.MINLOT || volume > (double)E.MAXLOT)
      {
        Logger.Error("open(): volume " + volume + " (made of " + _volume + ") out of bounds: " + ((double)E.MINLOT) + " " + ((double)E.MAXLOT));
        return null;
      }
      double price = normalize(_price);
      double stoploss = normalize(_stoploss);
      double takeprofit = normalize(_takeprofit);
      double SL_lim, TP_lim, SL_lim2, TP_lim2;
      if (cmd == TradeOperation.OP_BUY || cmd == TradeOperation.OP_SELL)
      {
        SL_lim = normalize(Position.getSL_lim(cmd));
        TP_lim = normalize(Position.getTP_lim(cmd));
        SL_lim2 = normalize(Position.getSL_lim2(cmd));
        TP_lim2 = normalize(Position.getTP_lim2(cmd));
      }
      else
      {
        double OP_lim = normalize(Position.getOP_lim(cmd));
        double OP_min = normalize(Position.getOP_min());
        SL_lim = normalize(Position.getSL_lim(cmd, (decimal)price));
        TP_lim = normalize(Position.getTP_lim(cmd, (decimal)price));
        SL_lim2 = normalize(Position.getSL_lim2(cmd, (decimal)price));
        TP_lim2 = normalize(Position.getTP_lim2(cmd, (decimal)price));
        if (cmd == TradeOperation.OP_BUYSTOP || cmd == TradeOperation.OP_SELLLIMIT)
        {
          if (Lib.comp(price, OP_lim) < 0)
          {
            Logger.Error("open(): wanted O/P " + price + " is lesser than border " + OP_lim + " so won't create an order");
            return null;
          }
        }
        else
        {
          if (Lib.comp(price, OP_lim) > 0)
          {
            Logger.Error("open(): wanted O/P " + price + " is greater than border " + OP_lim + " so won't create an order");
            return null;
          }
          if (Lib.comp(price, OP_min) < 0)
          {
            Logger.Error("open(): wanted O/P " + price + " is lesser than border " + OP_min + " so won't create an order");
            return null;
          }
        }
      }
      Enums.LimitedDirection _dir = Enums.LimitedDirection.Up;
      if (cmd == TradeOperation.OP_SELL || cmd == TradeOperation.OP_SELLSTOP || cmd == TradeOperation.OP_SELLLIMIT) _dir = Enums.LimitedDirection.Down;
      if (_dir == Enums.LimitedDirection.Up)
      {
        if (Lib.comp(stoploss, SL_lim) > 0)
        {
          Logger.Error("open(): wanted S/L " + stoploss + " is greater than border " + SL_lim + " so won't create an order");
          return null;
        }
        if (Lib.comp(takeprofit, TP_lim) < 0)
        {
          Logger.Error("open(): wanted T/P " + takeprofit + " is lesser than border " + TP_lim + " so won't create an order");
          return null;
        }
        if (Lib.comp(stoploss, SL_lim2) < 0)
        {
          Logger.Error("open(): wanted S/L " + stoploss + " is lesser than border " + SL_lim2 + " so won't create an order");
          return null;
        }
        if (Lib.comp(takeprofit, TP_lim2) > 0)
        {
          if (m_TP)
          {
            takeprofit = TP_lim2;
          }
          else
          {
            Logger.Error("open(): wanted T/P " + takeprofit + " is greater than border " + TP_lim2 + " so won't create an order");
            return null;
          }
        }
      }
      else
      {
        if (Lib.comp(stoploss, SL_lim) < 0)
        {
          Logger.Error("open(): wanted S/L " + stoploss + " is lesser than border " + SL_lim + " so won't create an order");
          return null;
        }
        if (Lib.comp(takeprofit, TP_lim) > 0)
        {
          Logger.Error("open(): wanted T/P " + takeprofit + " is greater than border " + TP_lim + " so won't create an order");
          return null;
        }
        if (Lib.comp(stoploss, SL_lim2) > 0)
        {
          Logger.Error("open(): wanted S/L " + stoploss + " is greater than border " + SL_lim2 + " so won't create an order");
          return null;
        }
        if (Lib.comp(takeprofit, TP_lim2) < 0)
        {
          if (m_TP)
          {
            takeprofit = TP_lim2;
          }
          else
          {
            Logger.Error("open(): wanted T/P " + takeprofit + " is lesser than border " + TP_lim2 + " so won't create an order");
            return null;
          }
        }
      }
      int slippage = half_normalize(_slippage);
      DateTime expiration = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      if (_valid_in_minutes >= 0)
      {
        // data wygaśnięcia (jesli nie zerowa) musi byc odległa o co najmniej 10 min i obcinane są sekundy i milisekundy
        expiration = E.TIME + new TimeSpan(0, _valid_in_minutes, 0);
        expiration -= new TimeSpan(0, 0, 0, expiration.Second, expiration.Millisecond);
      }
      Enums.MagicNumbers magic_num = Enums.MagicNumbers.Aggressive;
      if (_tactics == Enums.PositionTactics.Peak) magic_num = Enums.MagicNumbers.Peak;
      else if (_tactics == Enums.PositionTactics.Strict) magic_num = Enums.MagicNumbers.Strict;

      // magic number jest niewidoczny w interfejsie MT4 a dobrze jest jednak mieć opisane pozycje
      if (comment == "") comment = "magic: " + ((int)magic_num); else comment += " magic: " + ((int)magic_num);

      int ret = Position.orderSend(cmd, volume, price, slippage, stoploss, takeprofit, magic_num, comment, expiration);
      if (ret == -1)
      {
        Logger.Error("open(): couldn't create an order");
        return null;
      }
      if (ret == -2)
      {
        ret++;
        Logger.Error("open(): timeout during creating of an order so the result is unknown");
        status = Enums.PositionStatus.Unknown;
      }

      Position p = new Position(ret, E.TIME, E.tick_counter, _dir, cmd, volume, price, slippage, stoploss, takeprofit, expiration, _tactics, 0, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), stoploss, takeprofit, -1, 0, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, 0, 0, status, Enums.PositionProfitStatus.Losing, Enums.PositionProfitStatus.Losing, magic_num, comment);
      return p;
    }

    /// <summary>
    /// Zacieśnij maksymalnie S/L i T/P, zlecenie oczekujące skasuj.
    /// </summary>
    public void closeMild()
    {
      double SL_lim = normalize(getSL_lim());
      double TP_lim = normalize(getTP_lim());
      Logger.Warn("closeMild(): called for " + id + " S/L " + SL_lim + " T/P " + TP_lim);
      if (status == Enums.PositionStatus.Unknown)
      {
        // zlecenia dla unknown nie wysyłamy bo nawet numeru nie znamy
        Logger.Error("closeMild(): Unknown status so can't close a position");
      }
      else
      {
        bool modify = false;
        if (status == Enums.PositionStatus.Pending)
        {
          int ok = -1;
          if (!canBeDeleted())
          {
            Logger.Error("closeMild(): won't delete order " + id + " (FREEZELEVEL " + E.FREEZELEVEL + " open_price " + wanted_opening_price + ") but will try anyway");
          }
          try
          {
            ok = Position.orderDelete(id);
            last_modification_date = E.TIME;
          }
          catch
          {
            modify = true;
          }
          if (ok == -1) modify = true;
        }
        else if (status == Enums.PositionStatus.Opened)
        {
          modify = true;
        }
        if (modify)
        {
          try
          {
            double price = normalize((dir == Enums.LimitedDirection.Up ? E.BID : E.ASK));
            Position.orderModify(id, price, SL_lim, TP_lim);
            Logger.Warn("closeMild(): tried to modify " + id + " with S/L " + SL_lim + " T/P " + TP_lim);
            last_modification_date = E.TIME;
          }
          catch
          {
            // nie udało się to trudno
          }
        }
      }
    }

    /// <summary>
    /// Zamknij natychmiast (za wszelką cenę) o ile się da, zlecenie oczekujące skasuj. Nie uaktualniaj informacji o pozyzji.
    /// </summary>
    public void closeHarsh()
    {
      Logger.Warn("closeHarsh(): called for " + id);
      if (status == Enums.PositionStatus.Unknown)
      {
        // zlecenia dla unknown nie wysyłamy bo nawet numeru nie znamy
        Logger.Error("closeHarsh(): Unknown status so can't close a position");
      }
      else
      {
        bool modify = false;
        if (status == Enums.PositionStatus.Pending)
        {
          int ok = -1;
          if (!canBeDeleted())
          {
            Logger.Error("closeHarsh(): won't delete order " + id + " (FREEZELEVEL " + E.FREEZELEVEL + " open_price " + wanted_opening_price + ") but will try anyway");
          }
          try
          {
            ok = Position.orderDelete(id);
            last_modification_date = E.TIME;
          }
          catch
          {
            modify = true;
          }
          if (ok == -1) modify = true;
        }
        else if (status == Enums.PositionStatus.Opened)
        {
          modify = true;
        }
        if (modify)
        {
          double price = normalize((dir == Enums.LimitedDirection.Up ? E.BID : E.ASK));
          int ok = -1;
          if (!canBeClosed())
          {
            Logger.Error("closeHarsh(): won't close order " + id + " (FREEZELEVEL " + E.FREEZELEVEL + " SL " + SL + " TP " + TP + ") but will try anyway");
          }
          try
          {
            int slippage = Math.Max(3, half_normalize(E.getSlippage()));
            ok = Position.orderClose(id, volume, price, slippage);
            Logger.Warn("closeHarsh(): tried to close " + id + " with slippage " + slippage);
            last_modification_date = E.TIME;
          }
          catch
          {
            ok = -1;
          }
          if (ok == -1)
          {
            try
            {
              int slippage = half_normalize(Math.Max(E.TICKSIZE * 3.0M, E.POINT * Math.Max(E.FREEZELEVEL + 1, E.STOPLEVEL)) + E.getSlippage(histery_on_market: true));
              ok = Position.orderClose(id, volume, price, slippage);
              Logger.Warn("closeHarsh(): tried to close " + id + " with slippage " + slippage);
              last_modification_date = E.TIME;
            }
            catch
            {
              // nie udało się to trudno
            }
          }
        }
      }
    }


    /// <summary>
    /// Zwróć brzegową (najbliższą) dopuszczalną wartość żądanej ceny otwarcia.
    /// </summary>
    /// <param name="op">Rodzaj zlecenia.</param>
    /// <returns>Brzegowa dopuszczalna wartość żądanej ceny otwarcia.</returns>
    public static decimal getOP_lim(TradeOperation op)
    {
      decimal price = E.BID;
      if (op == TradeOperation.OP_BUY || op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP) price = E.ASK;
      decimal OP_lim = price;
      if (op == TradeOperation.OP_BUYLIMIT)
      {
        OP_lim = price - E.STOPLEVEL * E.POINT;
        if (Lib.comp(price - OP_lim, E.FREEZELEVEL * E.POINT) <= 0) OP_lim = price - (1 + E.FREEZELEVEL) * E.POINT;
      }
      else if (op == TradeOperation.OP_BUYSTOP)
      {
        OP_lim = price + E.STOPLEVEL * E.POINT;
        if (Lib.comp(OP_lim - price, E.FREEZELEVEL * E.POINT) <= 0) OP_lim = price + (1 + E.FREEZELEVEL) * E.POINT;
      }
      else if (op == TradeOperation.OP_SELLLIMIT)
      {
        OP_lim = price + E.STOPLEVEL * E.POINT;
        if (Lib.comp(OP_lim - price, E.FREEZELEVEL * E.POINT) <= 0) OP_lim = price + (1 + E.FREEZELEVEL) * E.POINT;
      }
      else if (op == TradeOperation.OP_SELLSTOP)
      {
        OP_lim = price - E.STOPLEVEL * E.POINT;
        if (Lib.comp(price - OP_lim, E.FREEZELEVEL * E.POINT) <= 0) OP_lim = price - (1 + E.FREEZELEVEL) * E.POINT;
      }
      return OP_lim;
    }

    /// <summary>
    /// Zwróć brzegową (najbliższą) dopuszczalną wartość żądanej ceny otwarcia.
    /// </summary>
    /// <returns>Brzegowa dopuszczalna wartość żądanej ceny otwarcia.</returns>
    public decimal getOP_lim()
    {
      if (status == Enums.PositionStatus.Opened)
      {
        return (decimal)opening_price;
      }
      else
      {
        return Position.getOP_lim(this.operation);
      }
    }

    /// <summary>
    /// Zwróć brzegowy (najbliższy) dopuszczalny poziom S/L.
    /// </summary>
    /// <param name="op">Rodzaj zlecenia.</param>
    /// <param name="opening_price">Żądana cena otwarcia (domyślnie -1, wtedy bierzemy cenę jak dla zleceń typu market).</param>
    /// <returns>Brzegowy dopuszczalny poziom S/L.</returns>
    public static decimal getSL_lim(TradeOperation op, decimal opening_price = -1)
    {
      decimal price = E.ASK;
      if (op == TradeOperation.OP_BUY || op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP) price = E.BID;
      if (opening_price > 0) price = opening_price;
      decimal SL_lim = 0;
      if (op == TradeOperation.OP_BUY)
      {
        SL_lim = price - E.STOPLEVEL * E.POINT;
        if (Lib.comp(price - SL_lim, E.FREEZELEVEL * E.POINT) <= 0) SL_lim = price - (1 + E.FREEZELEVEL) * E.POINT;
      }
      else if (op == TradeOperation.OP_SELL)
      {
        SL_lim = price + E.STOPLEVEL * E.POINT;
        if (Lib.comp(SL_lim - price, E.FREEZELEVEL * E.POINT) <= 0) SL_lim = price + (1 + E.FREEZELEVEL) * E.POINT;
      }
      else if (op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP)
      {
        SL_lim = price - E.STOPLEVEL * E.POINT;
      }
      else if (op == TradeOperation.OP_SELLLIMIT || op == TradeOperation.OP_SELLSTOP)
      {
        SL_lim = price + E.STOPLEVEL * E.POINT;
      }
      return SL_lim;
    }

    /// <summary>
    /// Zwróć brzegowy (najbliższy) dopuszczalny poziom S/L.
    /// </summary>
    /// <param name="opening_price">Żądana cena otwarcia (domyślnie -1, wtedy bierzemy starą żądaną cenę otwarcia). Ma znaczenie tylko dla Pending</param>
    /// <returns>Brzegowy dopuszczalny poziom S/L.</returns>
    public decimal getSL_lim(decimal opening_price = -1)
    {
      if (status == Enums.PositionStatus.Opened)
      {
        TradeOperation cur_op = (this.dir == Enums.LimitedDirection.Up ? TradeOperation.OP_BUY : TradeOperation.OP_SELL);
        return Position.getSL_lim(cur_op);
      }
      else
      {
        if (opening_price < 0) opening_price = (decimal)this.wanted_opening_price;
        return Position.getSL_lim(this.operation, opening_price);
      }
    }

    /// <summary>
    /// Zwróć brzegowy (najbliższy) dopuszczalny poziom T/P.
    /// </summary>
    /// <param name="op">Rodzaj zlecenia.</param>
    /// <param name="opening_price">Żądana cena otwarcia (domyślnie -1, wtedy bierzemy cenę jak dla zleceń typu market).</param>
    /// <returns>Brzegowy dopuszczalny poziom T/P.</returns>
    public static decimal getTP_lim(TradeOperation op, decimal opening_price = -1)
    {
      decimal price = E.ASK;
      if (op == TradeOperation.OP_BUY || op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP) price = E.BID;
      if (opening_price > 0) price = opening_price;
      decimal TP_lim = 0;
      if (op == TradeOperation.OP_BUY)
      {
        TP_lim = price + E.STOPLEVEL * E.POINT;
        if (Lib.comp(TP_lim - price, E.FREEZELEVEL * E.POINT) <= 0) TP_lim = price + (1 + E.FREEZELEVEL) * E.POINT;
      }
      else if (op == TradeOperation.OP_SELL)
      {
        TP_lim = price - E.STOPLEVEL * E.POINT;
        if (Lib.comp(price - TP_lim, E.FREEZELEVEL * E.POINT) <= 0) TP_lim = price - (1 + E.FREEZELEVEL) * E.POINT;
      }
      else if (op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP)
      {
        TP_lim = price + E.STOPLEVEL * E.POINT;
      }
      else if (op == TradeOperation.OP_SELLLIMIT || op == TradeOperation.OP_SELLSTOP)
      {
        TP_lim = price - E.STOPLEVEL * E.POINT;
      }
      return TP_lim;
    }

    /// <summary>
    /// Zwróć brzegowy (najbliższy) dopuszczalny poziom T/P.
    /// </summary>
    /// <param name="opening_price">Żądana cena otwarcia (domyślnie -1, wtedy bierzemy starą żądaną cenę otwarcia). Ma znaczenie tylko dla Pending</param>
    /// <returns>Brzegowy dopuszczalny poziom T/P.</returns>
    public decimal getTP_lim(decimal opening_price = -1)
    {
      if (status == Enums.PositionStatus.Opened)
      {
        TradeOperation cur_op = (this.dir == Enums.LimitedDirection.Up ? TradeOperation.OP_BUY : TradeOperation.OP_SELL);
        return Position.getTP_lim(cur_op);
      }
      else
      {
        if (opening_price < 0) opening_price = (decimal)this.wanted_opening_price;
        return Position.getTP_lim(this.operation, opening_price);
      }
    }

    /// <summary>
    /// Zwróć minimalną dopuszczalną wartość żądanej ceny otwarcia. Zawsze musimy mieć możliwość ustawienia stop lossu.
    /// </summary>
    /// <returns>Minimalna dopuszczalna wartość żądanej ceny otwarcia.</returns>
    public static decimal getOP_min()
    {
      decimal dif = (decimal)normalize(E.TICKSIZE / TAGeneralConfig.MAX_STOPLOSS_LIMIT) + E.TICKSIZE;
      decimal sl = E.STOPLEVEL * E.POINT;
      if (dif < sl) dif = sl;
      return E.TICKSIZE + dif;
    }

    /// <summary>
    /// Zwróć brzegowy (najdalszy) dopuszczalny poziom S/L.
    /// </summary>
    /// <param name="op">Rodzaj zlecenia.</param>
    /// <param name="opening_price">Żądana cena otwarcia (domyślnie -1, wtedy bierzemy cenę jak dla zleceń typu market).</param>
    /// <returns>Brzegowy dopuszczalny poziom S/L.</returns>
    public static decimal getSL_lim2(TradeOperation op, decimal opening_price = -1)
    {
      decimal price = E.ASK;
      if (op == TradeOperation.OP_BUY || op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP) price = E.BID;
      if (opening_price > 0) price = opening_price;
      decimal SL_lim = 0;
      decimal dist = price - E.TICKSIZE;
      dist *= TAGeneralConfig.MAX_STOPLOSS_LIMIT;
      if (op == TradeOperation.OP_BUY || op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP)
      {
        SL_lim = dist;
      }
      else
      {
        SL_lim = 2 * price - dist;
      }
      return SL_lim;
    }

    /// <summary>
    /// Zwróć brzegowy (najdalszy) dopuszczalny poziom S/L.
    /// </summary>
    /// <param name="opening_price">Żądana cena otwarcia (domyślnie -1, wtedy bierzemy starą żądaną cenę otwarcia). Ma znaczenie tylko dla Pending</param>
    /// <returns>Brzegowy dopuszczalny poziom S/L.</returns>
    public decimal getSL_lim2(decimal opening_price = -1)
    {
      if (status == Enums.PositionStatus.Opened)
      {
        TradeOperation cur_op = (this.dir == Enums.LimitedDirection.Up ? TradeOperation.OP_BUY : TradeOperation.OP_SELL);
        decimal dyn = Position.getSL_lim2(cur_op);
        decimal old = (decimal)this.SL;
        return (dyn <= old ? old : dyn);
      }
      else
      {
        if (opening_price < 0) opening_price = (decimal)this.wanted_opening_price;
        return Position.getSL_lim2(this.operation, opening_price);
      }
    }

    /// <summary>
    /// Zwróć brzegowy (najdalszy) dopuszczalny poziom T/P.
    /// </summary>
    /// <param name="op">Rodzaj zlecenia.</param>
    /// <param name="opening_price">Żądana cena otwarcia (domyślnie -1, wtedy bierzemy cenę jak dla zleceń typu market).</param>
    /// <returns>Brzegowy dopuszczalny poziom T/P.</returns>
    public static decimal getTP_lim2(TradeOperation op, decimal opening_price = -1)
    {
      decimal price = E.ASK;
      if (op == TradeOperation.OP_BUY || op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP) price = E.BID;
      if (opening_price > 0) price = opening_price;
      decimal TP_lim = 0;
      if (op == TradeOperation.OP_BUY || op == TradeOperation.OP_BUYLIMIT || op == TradeOperation.OP_BUYSTOP)
      {
        TP_lim = price * TAGeneralConfig.MAX_TAKEPROFIT_LIMIT;
      }
      else
      {
        TP_lim = E.TICKSIZE;
      }
      return TP_lim;
    }

    /// <summary>
    /// Zwróć brzegowy (najdalszy) dopuszczalny poziom T/P (górne ograniczenie dla pozycji otwartej zmienia się w zależności od aktualnej ceny).
    /// </summary>
    /// <param name="opening_price">Żądana cena otwarcia (domyślnie -1, wtedy bierzemy starą żądaną cenę otwarcia). Ma znaczenie tylko dla Pending</param>
    /// <returns>Brzegowy dopuszczalny poziom T/P.</returns>
    public decimal getTP_lim2(decimal opening_price = -1)
    {
      if (status == Enums.PositionStatus.Opened)
      {
        TradeOperation cur_op = (this.dir == Enums.LimitedDirection.Up ? TradeOperation.OP_BUY : TradeOperation.OP_SELL);
        return Position.getTP_lim2(cur_op);
      }
      else
      {
        if (opening_price < 0) opening_price = (decimal)this.wanted_opening_price;
        return Position.getTP_lim2(this.operation, opening_price);
      }
    }

    /// <summary>
    /// Normalizujemy liczbę decimal (kwantujemy po wartości quant, domyślnie E.POINT, zaokrąglając w dół!).
    /// </summary>
    /// <param name="x">Normalizowana liczba.</param>
    /// <param name="quant">Kwant wartości (gdy domyślne -1 - chodzi o E.POINT).</param>
    /// <returns>Znormalizowana liczba.</returns>
    public static double normalize(decimal x, decimal quant = -1)
    {
      if (quant < 0) quant = E.POINT;
      double _x = (double)(x / quant);
      _x = Lib.round(_x);
      _x = ((double)quant) * ((int)_x);
      return _x;
    }

    /// <summary>
    /// Ile wartości quant (domyślnie E.POINT) mieści się w danej liczbie.
    /// </summary>
    /// <param name="x">Badana liczba.</param>
    /// <param name="quant">Kwant wartości (gdy domyślne -1 - chodzi o E.POINT).</param>
    /// <returns>Liczba wartości POINT.</returns>
    public static int half_normalize(decimal x, decimal quant = -1)
    {
      if (quant < 0) quant = E.POINT;
      double _x = (double)(x / quant);
      _x = Lib.round(_x);
      return ((int)_x);
    }

    /// <summary>
    /// Zwróć XML opisujący pozycję.
    /// </summary>
    /// <param name="evolution">Czy umieszczać informacje o zmianach poziomów S/L i T/P.</param>
    /// <returns>XML opisujący pozycję.</returns>
    public string getXML(bool evolution = false)
    {
      double p = 0;
      if (status == Enums.PositionStatus.Closed)
      {
        p = closing_price;
      }
      else if (dir == Enums.LimitedDirection.Up)
      {
        p = (double)E.BID;
      }
      else
      {
        p = (double)E.ASK;
      }
      string str = "<position id=\"" + id + "\" volume=\"" + volume + "\" dir=\"" + dir + "\" status=\"" + status + "\" current_operation=\"" + current_operation + "\" tactics=\"" + tactics + "\" magic=\"" + ((int)magic_num) + "\" id_from_comment=\"" + id_from_comment + "\" symbol=\"" + symbol + "\" predecessor=\"" + predecessor + "\">\n";
      str += "\t<levels price=\"" + p + "\" SL=\"" + SL + "\" TP=\"" + TP + "\" BE=\"" + break_even + "\" bad_slippage=\"" + ((double)bad_slippage) + "\" max_loss_open=\"" + ((double)maxLossFromOpening) + "\" max_loss_cur=\"" + ((double)maxLossFromCurrent) + "\" min_price=\"" + ((double)low) + "\" max_price=\"" + ((double)height) + "\" pips_pot=\"" + pips_pot + "\"/>\n";
      str += "\t<creation local_time=\"" + creation_time_local + "\" time=\"" + creation_time + "\" tick=\"" + creation_tick + "\" operation=\"" + operation + "\" expiration=\"" + opening_expiration + "\" comment=\"" + opening_comment + "\"/>\n";
      if (operation != TradeOperation.OP_BUY && operation != TradeOperation.OP_SELL)
      {
        str += "\t<pending_time bars=\"" + age_before_opening + "\" min_meanwhile=\"" + ((double)min_in_age_before_opening) + "\" max_meanwhile=\"" + ((double)max_in_age_before_opening) + "\" imp_SR=\"" + ((double)imp_SR_for_strict) + "\"/>\n";
      }
      str += "\t<opening price=\"" + opening_price + "\" wanted_price=\"" + wanted_opening_price + "\" slippage=\"" + opening_slippage + "\" allowed_slippage=\"" + allowed_opening_slippage + "\" time=\"" + opening_time + "\" wanted_SL=\"" + wanted_opening_SL + "\" wanted_TP=\"" + wanted_opening_TP + "\"/>\n";
      str += "\t<closing price=\"" + closing_price + "\" wanted_price=\"" + wanted_closing_price + "\" slippage=\"" + closing_slippage + "\" allowed_slippage=\"" + allowed_closing_slippage + "\" time=\"" + closing_time + "\" closed_by_pos=\"" + closed_by + "\" successor=\"" + successor + "\" last_updated_tick=\"" + last_updated_tick + "\" pips=\"" + pips + "\"/>\n";
      decimal qp = (decimal)closing_price - (decimal)opening_price;
      if (dir == Enums.LimitedDirection.Down) qp = -qp;
      str += "\t<balance status=\"" + profit_status + "\" status_volatile=\"" + profit_status_volatile + "\" total=\"" + total_profit + "\" profit=\"" + profit + "\" quality=\"" + Math.Round(qp * 100 / bol_span) + "\"/>\n";
      decimal x = (decimal)(opening_price - wanted_opening_SL);
      if (x < 0) x = -x;
      int risk_pips = (int)((double)(x / E.POINT));
      str += "\t<risk amount=\"" + Math.Round(initialRisk, E.DIGITS) + "\" pips=\"" + risk_pips + "\" SL_to_VIM=\"" + Math.Round((x / initial_VIM), E.DIGITS) + "\" SL_to_STDDEV=\"" + Math.Round((x / initial_StdDev), E.DIGITS) + "\" SL_to_STDDEV_MA=\"" + Math.Round((x / initial_StdDev_MA), E.DIGITS) + "\" reward_to_risk=\"" + Math.Round((decimal)total_profit / initialRisk, E.DIGITS) + "\" reward_to_risk_pips=\"" + Math.Round((decimal)pips / risk_pips, E.DIGITS) + "\"/>\n";
      str += "\t<paid total=\"" + Math.Round(spread_costs + slippage_costs + (decimal)swap + (decimal)commission, E.DIGITS) + "\" spread_costs=\"" + Math.Round(spread_costs, E.DIGITS) + "\" slippage_costs=\"" + Math.Round(slippage_costs, E.DIGITS) + "\" swap=\"" + swap + "\" commission=\"" + commission + "\" price_per_costs=\"" + price_for_costs_normalized + "\"/>\n";
      str += "\t<opening_snapshot analysis=\"" + opening_snapshot.analysis + "\"/>\n";
      if (loose_trailing_squeezing)
      {
        str += "\t<trail-squeezing last_price=\"" + ((double)loose_trailing_squeezing_last_price) + "\" last_time=\"" + loose_trailing_squeezing_time + "\" sl_dist=\"" + ((double)loose_trailing_squeezing_sl_dist) + "\"/>\n";
      }
      if (beingSqeezed)
      {
        str += "\t<squeezing since=\"" + beingSqeezed_date + "\" tick=\"" + beingSqeezed_tick + "\" price=\"" + beingSqeezed_price + "\"";
        if (beingTightlySqeezed)
        {
          str += " tight_since=\"" + beingTightlySqeezed_date + "\" tick=\"" + beingTightlySqeezed_tick + "\" tight_price=\"" + beingTightlySqeezed_price + "\"";
        }
        str += "/>\n";
      }
      if (evolution)
      {
        if (params_changes.Count() == 0)
        {
          str += "\t<changes count=\"0\"/>\n";
        }
        else
        {
          if (DebugLogConfig.DEBUG_EVOLUTION_SIZE)
          {
#pragma warning disable 0162
            string debstr = "DEBUG_EVOLUTION_SIZE: " + id + " " + params_changes.Count();
            if (params_changes.Count() > 2)
            {
              debstr += " " + params_changes[0].Item2 + " " + params_changes[1].Item2 + " " + params_changes[2].Item2;
            }
            Console.WriteLine(debstr);
#pragma warning restore 0162
          }
          str += "\t<changes count=\"" + params_changes.Count() + "\">\n";
          for (int i = 0; i < params_changes.Count(); i++)
          {
            str += "\t\t<change tick=\"" + params_changes[i].Item1 + "\" time=\"" + params_changes[i].Item2 + "\" SL=\"" + params_changes[i].Item3 + "\" TP=\"" + params_changes[i].Item4 + "\"/>\n";
          }
          str += "\t</changes>\n";
        }
      }
      str += "</position>";
      return str;
    }
  }
}
