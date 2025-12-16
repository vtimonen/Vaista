using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Xml.Schema;
using Jypeli;
using Jypeli.Assets;
using Jypeli.Controls;
using Jypeli.Widgets;

namespace Vaista;

/// @author Valtteri Timonen
/// @version 22.11.2024
/// <summary>
/// Vaista (Bullet Hell-peli)
/// </summary>
public class Vaista : PhysicsGame
{
    private static readonly Image _taustakuva = LoadImage("night_background1.0.png");       // pelin taustakuva

    private Label _laskuri;
    private double _aika = 50;      // laskurin alkuarvo
    private Timer _ajastin;
    
    private PhysicsObject _pelaaja;         // pelaaja
    private double _liike = 275;        // pelaajan nopeus
    
    private double _ampumisNopeus = 350;        // ammuksen nopeus

    private static readonly List<Image> _vihollisTyypit =       // lista vihujen eri spriteista
    [
        LoadImage("vihu1.png"),
        LoadImage("vihu2.png"),
        LoadImage("vihu3.png")
    ];
    
    private int _elamat = 1;        // pelaajan elamien maara
    private Label _elamatLabel;     // elamalaskuri

    private static readonly SoundEffect _ammuAani = LoadSoundEffect("laser_gun.wav");       // ampumisaani

    /// <summary>
    /// Paaohjelma, jossa kutsutaan pelin kokonaisuudessaan luovia aliohjelmia, seka asetetaan ajastimelle tarvittavat
    /// asetukset. Ohjaimissa kutsutaan Z-napilla Ammu-aliohjelmaa ja escapella saadaan pelin lopetus overlay nakyviin
    /// </summary>
    public override void Begin()
    {
        LuoKentta();
        LuoPelaaja();
        AsetaOhjaimet();
        LuoLaskuri();
        LuoElamat();
        LuoVihollinen();
        ElamienKasvatus();
        
        _ajastin = new Timer();
        _ajastin.Interval = 1;
        _ajastin.Timeout += LaskeAika;
        _ajastin.Start();
        
        Keyboard.Listen(Key.Z, ButtonState.Pressed, Ammu, "Pelaaja ampuu");
        Keyboard.Listen(Key.Escape, ButtonState.Pressed, ConfirmExit, "Lopeta peli");
    }
    
    
    /// <summary>
    /// Aliohjelma luo pelaajan koon, asettaa sille kuvan, paikan ja pitaa kuvan MomentOfInertialla pystysuorassa
    /// vaikka mika osuisi pelaajaan. Aliohjelmassa on myos collisionhandler pelaajan ja vihollisen valilla.
    /// </summary>
    private void LuoPelaaja()
    {
        _pelaaja = new PhysicsObject(40, 40);         // pelaajan koko
        Image pelaajaKuva = LoadImage("pelaaja.png");      // pelaajan sprite
        _pelaaja.Image = pelaajaKuva;
        _pelaaja.Position = new Vector(0, -250);
        _pelaaja.Restitution = 0.0;                             // pelaaja ei pompi
        _pelaaja.MomentOfInertia = Double.PositiveInfinity;     // esta pyoriminen
        Add(_pelaaja);
        
        AddCollisionHandler(_pelaaja, VihollinenTormasiP);
    }
    
    
    /// <summary>
    /// Aliohjelma odottaa kymmenen sekuntia paaohjelman kaynnistyksesta, jonka jalkeen pelaajalle lisataan
    /// yksi elama lisaa niin kauan, kunnes peli paattyy.
    /// </summary>
    private async void ElamienKasvatus()
    {                             
        while (true)
        {
            
            await Task.Delay(10000);
            
            _elamat++;
            _elamatLabel.Text = "Elämät: " + _elamat;
            
        }
    }
    
    
    /// <summary>
    /// Aliohjelma luo peliin reunat ja asettaa taustakuvan. Aliohjelma kaynnistaa pelin aikana soivan musiikin
    /// ja toistaa sita aanitiedostoa, jos se ehtisi loppua ennen pelin loppumista.
    /// Aliohjelma kutsuu LuoReuna-funktiota kahdesti: kerran ylareunalle ja kerran alareunalle.
    /// </summary>
    private void LuoKentta()      
    {
        Level.CreateBorders();      
        Level.Background.Image = _taustakuva;
        
        MediaPlayer.Play("Stage1.wav");
        MediaPlayer.IsRepeating = true;
                                            
        PhysicsObject ylaReuna = LuoReuna(20, new Vector(0, Level.Top - 10), Color.Gray, "reuna");
        PhysicsObject alaReuna = LuoReuna(20, new Vector(0, Level.Bottom + 10), Color.Gray, "reuna");
        
        Add(ylaReuna);
        Add(alaReuna);
    }
    
    
    /// <summary>
    /// Funktio, joka palauttaa reunan jota kutsutaan LuoKentta-aliohjelmassa
    /// </summary>
    /// <param name="korkeus">reunan korkeuden pituus</param>
    /// <param name="sijainti">mihin reuna sijoitetaan</param>
    /// <param name="vari">mika reunan variksi tulee</param>
    /// <param name="tag">tagi helpottamaan collisionhandlereita</param>
    /// <returns>reunan, jota kutsutaan LuoKentta-alihojelmassa</returns>
    private PhysicsObject LuoReuna(double korkeus, Vector sijainti, Color vari, string tag)
    {
        PhysicsObject reuna = PhysicsObject.CreateStaticObject(Level.Width, korkeus);
        reuna.Position = sijainti; 
        reuna.Color = vari;        
        reuna.Tag = tag;           
        return reuna;              
    }

    
    /// <summary>
    /// Alihojelma asettaa pelaajalle ohjaimet, painaessa pelaaja liikkuu ja irti paastettya pysahtyy heti, ei liu'u
    /// </summary>
    private void AsetaOhjaimet()
    {
        Keyboard.Listen(Key.Up, ButtonState.Down, LiikutaPelaajaa, "Ylos", new Vector(0, _liike));
        Keyboard.Listen(Key.Down, ButtonState.Down, LiikutaPelaajaa, "Alas", new Vector(0, -_liike));
        Keyboard.Listen(Key.Left, ButtonState.Down, LiikutaPelaajaa, "Vasemmalle", new Vector(-_liike, 0));
        Keyboard.Listen(Key.Right, ButtonState.Down, LiikutaPelaajaa, "Oikealle", new Vector(_liike, 0));

        Keyboard.Listen(Key.Up, ButtonState.Released, Pysayta, "Pysaytaliike");
        Keyboard.Listen(Key.Down, ButtonState.Released, Pysayta, "Pysaytaliike");
        Keyboard.Listen(Key.Left, ButtonState.Released, Pysayta, "Pysaytaliike");
        Keyboard.Listen(Key.Right, ButtonState.Released, Pysayta, "Pysaytaliike");
    }

    
    /// <summary>
    /// Alihojelmaa kutsutaan AsetaOhjaimet-aliohjelmassa, kun painetaan joko ylos, alas, vasen tai oikea
    /// Taman johdosta pelaaja liikkuu parametrin asettamaan suuntaan
    /// </summary>
    /// <param name="suunta">arvo, joka maarittaa mihin suuntaan pelaaja liikkuu</param>
    private void LiikutaPelaajaa(Vector suunta)
    {
        _pelaaja.Velocity = suunta;
    }

    
    /// <summary>
    /// Alihojelma varmistaa, etta pelaaja ei liu'u, kun pelaajan liikutus lopetetaan, vaan se pysahtyy valittomasti
    /// </summary>
    private void Pysayta()
    {
        _pelaaja.Velocity = Vector.Zero;
    }

    
    /// <summary>
    /// Alihojelmassa luodaan luoti, sen koko, muoto, vari, kohta josta luoti lahtee joka on pelaajasta hieman ylospain
    /// luodin suunta ja alihojelmassa myos kutsutaan aaniefektia, joka ampumisesta kuuluu.
    /// Aliohjelmassa on myos collisionhandler ammuksen ja ylareunan valilla.
    /// </summary>
    private void Ammu()
    {
        PhysicsObject ammus = new PhysicsObject(10, 10);
        ammus.Shape = Shape.Circle;
        ammus.Color = Color.Yellow;
        ammus.Position = _pelaaja.Position + new Vector(0, 30);
        ammus.Restitution = 0.0;
        ammus.Velocity = new Vector(0, _ampumisNopeus);
        Add(ammus);

        _ammuAani.Play();
        
        AddCollisionHandler(ammus, AmmusTormasi);
    }
    
    
    /// <summary>
    /// Aliohjelma kasittelee mita tapahtuu kun ammus tormaa ylareunaan. Eli poistaa ammuksen kokonaan.
    /// </summary>
    /// <param name="ammus">pelaajan ampuma physicsobject</param>
    /// <param name="ylaReuna">pelin ylareuna</param>
    private void AmmusTormasi(PhysicsObject ammus, PhysicsObject ylaReuna)
    {
       Remove(ammus);
    }

    
    /// <summary>
    /// Vahentaa _aika-muuttujasta sekunnin joka sekunti ja peli paattyy, kun sekunteja on vahemman kuin 0.
    /// Sekuntien vaheneminen paivitetaan myos joka kerta, kun muuttujasta vahennetaan sekunti.
    /// </summary>
    private void LaskeAika()
    {                               
        _aika--;

        if (_aika < 0)
        {
            _aika = 0;
            PeliLoppuu();
        }
        
        _laskuri.Text = "Aika: " + Math.Floor(_aika).ToString();
    }

    
    /// <summary>
    /// Aliohjelma on vaihtoehtoinen loppu, jos pelaaja selviaa 50 sekuntia pelissa. Alihojelma poistaa kaiken ruudulta,
    /// pysayttaa musiikin. Soittaa voittomusiikin, eika toista sita. Lataa uuden kuvan, jossa lukee "Voitit pelin!"
    /// Pelin paattyessa tahan on mahdollista lopettaa peli esc valikon kautta.
    /// </summary>
    private void PeliLoppuu()
    {
        ClearAll();     
        MediaPlayer.Stop();     
        
        MediaPlayer.Play("Victory.wav");        
        MediaPlayer.IsRepeating = false;        
        
        Image gameover = LoadImage("gameover.png");     
                                                            
        GameObject loppu = new GameObject(Level.Width, Level.Height);       
        loppu.Image = gameover;
        Add(loppu);
        
        Keyboard.Listen(Key.Escape, ButtonState.Pressed, ConfirmExit, "Lopeta peli");
    }
    
    
    /// <summary>
    /// Aliohjelma luo tekstikentan ajalle ja maarittaa mita tekstikentassa nakyy. Tekstikentalle sijoitetaan
    /// sijainti ja taustavari, seka se lisataan peliin.
    /// </summary>
    private void LuoLaskuri()       // tekstikentta ajalle
    {
        _laskuri = new Label("Aika: " + _aika);     
        _laskuri.Position = new Vector(375, 325);       
        _laskuri.Color = Color.LightGreen;              
        
        Add(_laskuri);
    }

    
    /// <summary>
    /// Listaan satunnainenKuva on tallennettu kolme spritea, josta aliohjelma valitsee satunnaisesti jonkun
    /// sitten lisaa vihollisia Timer.SingleShot kohdassa joka sekunti maariteltyihin paikkoihin randomisti
    /// viholliset liikkuvat tasaisesti alaspain
    /// Aliohjelma sisaltaa kaksi collision handleria. Ensimmainen kasittelee vihollisen ja pelaajan tormaysta, toinen
    /// kasittelee vihollisen ja alareunan tormaysta.
    /// </summary>
    private void LuoVihollinen()
    {
        Image satunnainenKuva = _vihollisTyypit[RandomGen.NextInt(0, _vihollisTyypit.Count)];
        PhysicsObject vihollinen = new PhysicsObject(50, 50);
        vihollinen.Image = satunnainenKuva;
        vihollinen.Position = new Vector(RandomGen.NextDouble(-Level.Width / 3, Level.Width / 3), Level.Top - 100);
        vihollinen.Velocity = new Vector(0, -150);
        vihollinen.Restitution = 0.0;
        vihollinen.Tag = "vihollinen";
        Add(vihollinen);

        Timer.SingleShot(RandomGen.NextDouble(1, 1), LuoVihollinen);
        
        AddCollisionHandler(vihollinen, _pelaaja, CollisionHandler.DestroyObject);
        AddCollisionHandler(vihollinen, VihollinenTormasi);
    }
    
    
    /// <summary>
    /// Aliohjelma maarittaa mita tapahtuu kun vihollinen tormaa alareunaan. Vihollinen poistetaan ruudulta
    /// </summary>
    /// <param name="vihollinen">pelin vihollinen</param>
    /// <param name="alaReuna">pelin alareuna</param>
    private void VihollinenTormasi(PhysicsObject vihollinen, PhysicsObject alaReuna)
    {
        Remove(vihollinen);     // vihollinen poistetaan, kun se osuu alareunaan
    }

    
    /// <summary>
    /// Aliohjelma maarittelee mita tapahtuu kun pelaaja osuu viholliseen.
    /// Kun pelaaja osuu viholliseen, pelaajalta vahennetaan yksi elama.
    /// </summary>
    /// <param name="pelaajaosuu">Pelin pelaaja</param>
    /// <param name="vihu">Pelaajan vihollinen</param>
    private void VihollinenTormasiP(PhysicsObject pelaajaosuu, PhysicsObject vihu)
    {
        if (vihu.Tag.ToString() == "vihollinen")
        {
            VahennaElama();     
        }
        
    }
    
    
    /// <summary>
    /// Alihojelma luo tekstikentan pelaajan elamille, maarittaa sille sisallon ja sijainnin seka tekstikentan varin.
    /// </summary>
    private void LuoElamat()        
    {
        _elamatLabel = new Label("Elämät: " + _elamat);     
        _elamatLabel.Position = new Vector(375, 300);       
        _elamatLabel.Color = Color.Red;     
        Add(_elamatLabel);
    }
    
    
    /// <summary>
    /// Aliohjelma vahentaa pelaajalta elaman aina kun pelaaja osuu viholliseen ja paivittaa tekstikentan.
    /// Jos pelaajan elamat loppuvat, peli kutsuu HavisitPelin()-aliohjelmaa, joka on toinen vaihtoehtoinen
    /// loppu pelille.
    /// </summary>
    private void VahennaElama()
    {
        _elamat--;                                  
        _elamatLabel.Text = "Elämät: " + _elamat;   

        if (_elamat == 0)       
        {
            HavisitPelin();
        }
    }
    
    
    /// <summary>
    /// Toinen vaihtoehtoinen loppu pelille, joka saavutetaan menettamalla pelaajan kaikki elamat.
    /// Aliohjelma poistaa kaiken ruudulta ja pysayttaa pelin musiikin.
    /// Pelissa alkaa soimaan haviomusiikki joka ei toistu. Aliohjelma lataa havisit-kuvan, jossa lukee "Havisit pelin!"
    /// Pelin paattyessa tahan on mahdollista lopettaa peli esc valikon kautta.
    /// </summary>
    private void HavisitPelin()     // vaihtoehtoinen loppu pelille, jos pelaaja osuu liian moneen viholliseen
    {
        ClearAll();     // poistaa kaiken ruudulta
        MediaPlayer.Stop();     // musiikki pysahtyy
        
        MediaPlayer.Play("GameOver.wav");       // haviomusa
        MediaPlayer.IsRepeating = false;        // ei toistu
        
        Image gameover = LoadImage("havisit.png");      // kuva, jossa lukee havisit pelin
        GameObject loppu = new GameObject(Level.Width, Level.Height);
        loppu.Image = gameover;
        Add(loppu);
        
        Keyboard.Listen(Key.Escape, ButtonState.Pressed, ConfirmExit, "Lopeta peli");
    }
}