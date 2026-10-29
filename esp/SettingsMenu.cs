namespace CsEsp;
internal sealed class SettingsMenu : Form
{
    readonly Func<EspSettings> current;
    readonly Action<EspSettings> change;
    readonly Action<string,string> command;
    readonly Func<string,string> information;
    readonly Func<GrenadeSpot[]> spots;
    readonly Label footer;
    readonly List<(Control Control,Action Sync)> bindings=[];
    readonly System.Windows.Forms.Timer refresh=new(){Interval=700};
    bool refreshing;
    string profileKey="Rifles";
    readonly Color surface=Color.FromArgb(19,24,28),panel=Color.FromArgb(25,32,37),ink=Color.FromArgb(231,240,243),muted=Color.FromArgb(160,184,194),cyan=Color.FromArgb(65,217,230);
    internal SettingsMenu(Func<EspSettings> get,Action<EspSettings> set,bool standalone=false,Action<string,string>? action=null,Func<string,string>? info=null,Func<GrenadeSpot[]>? grenadeSpots=null)
    {
        current=get;change=set;command=action??((_,_)=>{});information=info??(_=>"Sin iniciar");spots=grenadeSpots??(()=>[]);
        Text="Nexo · Panel de control";Size=new Size(1050,790);MinimumSize=new Size(1000,700);StartPosition=FormStartPosition.CenterScreen;
        BackColor=surface;ForeColor=ink;Font=new Font("Segoe UI",9);MaximizeBox=false;MinimizeBox=false;TopMost=true;KeyPreview=true;ShowInTaskbar=false;
        footer=new Label{Text="Cambios guardados automáticamente · Insert: volver al juego",Dock=DockStyle.Bottom,Height=40,Padding=new Padding(20,10,12,8),ForeColor=muted};Controls.Add(footer);
        var navigation=new FlowLayoutPanel{Dock=DockStyle.Left,Width=174,BackColor=Color.FromArgb(15,21,25),FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(14,18,10,16)};Controls.Add(navigation);
        navigation.Controls.Add(new Label{Text="NEXO",ForeColor=cyan,Font=new Font("Segoe UI",25,FontStyle.Bold),Width=145,Height=62});
        navigation.Controls.Add(new Label{Text="Panel de control",ForeColor=muted,Width=145,Height=35});
        var body=new Panel{Dock=DockStyle.Fill};Controls.Add(body);body.BringToFront();
        var tabs=new TabControl{Dock=DockStyle.Fill,Appearance=TabAppearance.FlatButtons,SizeMode=TabSizeMode.Fixed,ItemSize=new Size(1,1),Multiline=true};body.Controls.Add(tabs);
        var pages=new[]{"Apuntado","Visuales","Radar","Mundo","Granadas","Configuración"}.Select(title=>Page(tabs,title)).ToArray();
        var navButtons=new List<Button>();
        for(int i=0;i<pages.Length;i++)
        {
            int index=i;var b=new Button{Text=tabs.TabPages[i].Text,Width=145,Height=43,FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0),BackColor=surface,ForeColor=ink,Margin=new Padding(0,3,0,3)};b.FlatAppearance.BorderSize=0;
            b.Click+=(_,_)=>tabs.SelectedIndex=index;navigation.Controls.Add(b);navButtons.Add(b);
        }
        tabs.SelectedIndexChanged+=(_,_)=>{for(int i=0;i<navButtons.Count;i++){navButtons[i].BackColor=i==tabs.SelectedIndex?Color.FromArgb(30,63,70):surface;navButtons[i].ForeColor=i==tabs.SelectedIndex?cyan:ink;}};
        navButtons[0].BackColor=Color.FromArgb(30,63,70);navButtons[0].ForeColor=cyan;
        BuildAim(pages[0]);BuildVisuals(pages[1]);BuildRadar(pages[2]);BuildWorld(pages[3]);BuildGrenades(pages[4]);BuildConfig(pages[5]);
        KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape){Hide();e.Handled=true;}};
        VisibleChanged+=(_,_)=>{if(Visible)Synchronize();};refresh.Tick+=(_,_)=>{if(Visible)foreach(var label in webLabels)label.Text=information("web");};refresh.Start();
        FormClosing+=(_,e)=>{if(!standalone&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
    }
    FlowLayoutPanel Page(TabControl tabs,string title)
    {
        var page=new TabPage(title){BackColor=surface,ForeColor=ink};tabs.TabPages.Add(page);var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(16,14,10,18)};page.Controls.Add(flow);return flow;
    }
    FlowLayoutPanel Group(FlowLayoutPanel page,string title)
    {
        var flow=new FlowLayoutPanel{BackColor=panel,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(370,40),MaximumSize=new Size(370,0),Padding=new Padding(14,12,14,14),Margin=new Padding(0,0,14,16)};
        var heading=new Label{Text=title,ForeColor=cyan,Font=new Font("Segoe UI",11,FontStyle.Bold),Width=340,Height=30,Margin=new Padding(0,0,0,8)};flow.Controls.Add(heading);page.Controls.Add(flow);return flow;
    }
    void Bind(Control c,Action sync)=>bindings.Add((c,sync));
    internal void Synchronize(){refreshing=true;try{bindings.RemoveAll(b=>b.Control.IsDisposed);foreach(var b in bindings)b.Sync();}finally{refreshing=false;}}
    void Note(FlowLayoutPanel page,string text)
    {
        int height=TextRenderer.MeasureText(text,Font,new Size(335,0),TextFormatFlags.WordBreak).Height+12;page.Controls.Add(new Label{Text=text,Width=340,Height=height,ForeColor=muted,Margin=new Padding(0,3,0,8)});
    }
    void Toggle(FlowLayoutPanel page,string text,Func<EspSettings,bool> get,Func<EspSettings,bool,EspSettings> set)
    {
        var box=new CheckBox{Text=text,Checked=get(current()),Width=338,Height=28,Margin=new Padding(0,0,0,2),AccessibleName=text};Bind(box,()=>box.Checked=get(current()));box.CheckedChanged+=(_,_)=>{if(!refreshing)change(set(current(),box.Checked));};page.Controls.Add(box);
    }
    void Number(FlowLayoutPanel page,string text,decimal min,decimal max,Func<EspSettings,decimal> get,Func<EspSettings,decimal,EspSettings> set,decimal step=1,int decimals=0)
    {
        var row=new Panel{Width=340,Height=36,Margin=new Padding(0,2,0,2)};row.Controls.Add(new Label{Text=text,Location=new Point(0,8),Size=new Size(235,26)});
        var value=new NumericUpDown{Minimum=min,Maximum=max,Value=Math.Clamp(get(current()),min,max),Increment=step,DecimalPlaces=decimals,Location=new Point(240,4),Size=new Size(94,26),BackColor=surface,ForeColor=ink,AccessibleName=text};
        Bind(value,()=>value.Value=Math.Clamp(get(current()),min,max));value.ValueChanged+=(_,_)=>{if(!refreshing)change(set(current(),value.Value));};row.Controls.Add(value);page.Controls.Add(row);
    }
    void ColorButton(FlowLayoutPanel page,string text,Func<EspSettings,int> get,Func<EspSettings,int,EspSettings> set)
    {
        var row=new Panel{Width=340,Height=34,Margin=new Padding(0,1,0,1)};row.Controls.Add(new Label{Text=text,Location=new Point(0,8),Size=new Size(260,25)});
        var button=new Button{BackColor=Color.FromArgb(get(current())),Size=new Size(55,23),Location=new Point(278,4),FlatStyle=FlatStyle.Flat,AccessibleName=text};button.FlatAppearance.BorderColor=muted;Bind(button,()=>button.BackColor=Color.FromArgb(get(current())));
        button.Click+=(_,_)=>{using var dialog=new ColorDialog{Color=Color.FromArgb(get(current())),FullOpen=true};if(dialog.ShowDialog(this)==DialogResult.OK){change(set(current(),dialog.Color.ToArgb()));button.BackColor=dialog.Color;}};row.Controls.Add(button);page.Controls.Add(row);
    }
    void Key(FlowLayoutPanel group,string text,Func<EspSettings,int> get,Func<EspSettings,int,EspSettings> set)
    {
        var row=new Panel{Width=340,Height=36};row.Controls.Add(new Label{Text=text,Location=new Point(0,8),Size=new Size(210,24)});
        var combo=new ComboBox{Location=new Point(212,3),Width=122,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=surface,ForeColor=ink,AccessibleName=text};
        var keys=Enumerable.Range(1,254).Select(v=>new KeyOption(v,((Keys)v).ToString())).ToArray();combo.DataSource=keys;combo.DisplayMember="Name";combo.ValueMember="Value";combo.SelectedValue=get(current());Bind(combo,()=>combo.SelectedValue=get(current()));combo.SelectedValueChanged+=(_,_)=>{if(!refreshing&&combo.SelectedValue is int value)change(set(current(),value));};row.Controls.Add(combo);group.Controls.Add(row);
    }
    record KeyOption(int Value,string Name);
    Button ActionButton(FlowLayoutPanel group,string title,Action action)
    {
        var b=new Button{Text=title,Width=338,Height=33,FlatStyle=FlatStyle.Flat,BackColor=surface,ForeColor=ink,Margin=new Padding(0,5,0,3)};b.FlatAppearance.BorderColor=Color.FromArgb(57,86,96);b.Click+=(_,_)=>{try{action();}catch(Exception ex){Feedback(ex.Message);}};group.Controls.Add(b);return b;
    }
    TextBox TextEntry(FlowLayoutPanel group,string label,string value="")
    {
        group.Controls.Add(new Label{Text=label,Width=340,Height=23,Margin=new Padding(0,7,0,0)});var text=new TextBox{Text=value,Width=335,BackColor=surface,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,AccessibleName=label};group.Controls.Add(text);return text;
    }
    ComboBox Choice(FlowLayoutPanel group,string label,string[] values)
    {
        group.Controls.Add(new Label{Text=label,Width=340,Height=23,Margin=new Padding(0,6,0,0)});var c=new ComboBox{Width=335,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=surface,ForeColor=ink,AccessibleName=label};c.Items.AddRange(values);if(values.Length>0)c.SelectedIndex=0;group.Controls.Add(c);return c;
    }
    AimProfile SelectedProfile()
    {
        if(current().AimProfiles?.TryGetValue(profileKey,out var p)==true)return p.Normalize();
        return profileKey.StartsWith("arma:")&&ushort.TryParse(profileKey[5..],out var id)?current().Profile(id):new();
    }
    EspSettings SetProfile(Func<AimProfile,AimProfile> modify)
    {
        var profiles=new Dictionary<string,AimProfile>(current().AimProfiles??[]){[profileKey]=modify(SelectedProfile()).Normalize()};return current() with{AimProfiles=profiles};
    }
    void BuildAim(FlowLayoutPanel page)
    {
        var select=Group(page,"Perfil por arma");Note(select,"Los perfiles individuales tienen prioridad sobre su categoría. El apuntado requiere mantener la tecla configurada.");
        string[] groups=["Rifles","SMG","Pistolas","Francotiradores","Escopetas","Pesadas","Otras"];
        var options=groups.Select(g=>(Key:g,Label:g)).Concat(new ushort[]{1,2,3,4,7,8,9,10,11,13,14,16,17,19,23,24,25,26,27,28,29,30,32,33,34,35,36,38,39,40,60,61,63,64}.Select(id=>(Key:"arma:"+id,Label:WeaponNames.Label(id)))).ToArray();
        var chooser=Choice(select,"Editar perfil",options.Select(o=>o.Label).ToArray());
        Toggle(select,"Detectar sensibilidad guardada de CS2",s=>s.AutoSensitivity,(s,v)=>s with{AutoSensitivity=v});
        Number(select,"Sensibilidad manual / respaldo",.01m,20,s=>(decimal)s.Sensitivity,(s,v)=>s with{Sensitivity=(float)v},.01m,2);
        Note(select,"Visibilidad geométrica: no implementada. El estado de radar no equivale a una línea de visión comprobada.");
        var aim=Group(page,"Apuntado");
        void Fields()
        {
            while(aim.Controls.Count>1){var c=aim.Controls[1];aim.Controls.Remove(c);c.Dispose();}
            Toggle(aim,"Activar apuntado",_=>SelectedProfile().Enabled,(_,v)=>SetProfile(p=>p with{Enabled=v}));Key(aim,"Tecla de apuntado",_=>SelectedProfile().Key,(_,v)=>SetProfile(p=>p with{Key=v}));
            Number(aim,"FOV angular (grados)",.1m,30,_=>(decimal)SelectedProfile().Fov,(_,v)=>SetProfile(p=>p with{Fov=(float)v}),.1m,1);
            Number(aim,"Suavizado",1,50,_=>(decimal)SelectedProfile().Smooth,(_,v)=>SetProfile(p=>p with{Smooth=(float)v}));Toggle(aim,"Suavizado lineal",_=>SelectedProfile().Linear,(_,v)=>SetProfile(p=>p with{Linear=v}));
            Toggle(aim,"Hueso más cercano",_=>SelectedProfile().Closest,(_,v)=>SetProfile(p=>p with{Closest=v}));
            foreach(var(flag,label) in new[]{(Hitboxes.Head,"Cabeza"),(Hitboxes.Neck,"Cuello"),(Hitboxes.Chest,"Pecho"),(Hitboxes.Pelvis,"Pelvis"),(Hitboxes.Legs,"Piernas")})
                Toggle(aim,"Objetivo: "+label,_=>SelectedProfile().Hitboxes.HasFlag(flag),(_,v)=>SetProfile(p=>p with{Hitboxes=v?p.Hitboxes|flag:p.Hitboxes&~flag}));
            Toggle(aim,"Mostrar FOV",_=>SelectedProfile().ShowFov,(_,v)=>SetProfile(p=>p with{ShowFov=v}));ColorButton(aim,"Color del FOV",_=>SelectedProfile().FovColor,(_,v)=>SetProfile(p=>p with{FovColor=v}));
            Toggle(aim,"FOV sigue el retroceso",_=>SelectedProfile().FollowRecoil,(_,v)=>SetProfile(p=>p with{FollowRecoil=v}));ColorButton(aim,"Color al seguir retroceso",_=>SelectedProfile().RecoilColor,(_,v)=>SetProfile(p=>p with{RecoilColor=v}));
        }
        Fields();chooser.SelectedIndexChanged+=(_,_)=>{profileKey=options[chooser.SelectedIndex].Key;Fields();Synchronize();};
        var recoil=Group(page,"Retroceso");Toggle(recoil,"Activar RCS",_=>SelectedProfile().Rcs,(_,v)=>SetProfile(p=>p with{Rcs=v}));Number(recoil,"Compensación horizontal",0,3,_=>(decimal)SelectedProfile().RecoilX,(_,v)=>SetProfile(p=>p with{RecoilX=(float)v}),.05m,2);Number(recoil,"Compensación vertical",0,3,_=>(decimal)SelectedProfile().RecoilY,(_,v)=>SetProfile(p=>p with{RecoilY=(float)v}),.05m,2);Note(recoil,"Desactivado inicialmente. Requiere validar el comportamiento al disparar en tu partida de práctica.");
        var trigger=Group(page,"Disparo y Trigger Aim");Toggle(trigger,"Activar triggerbot",_=>SelectedProfile().Trigger,(_,v)=>SetProfile(p=>p with{Trigger=v}));Key(trigger,"Tecla de disparo asistido",_=>SelectedProfile().TriggerKey,(_,v)=>SetProfile(p=>p with{TriggerKey=v}));Number(trigger,"Retardo de disparo (ms)",0,1000,_=>SelectedProfile().ShootDelay,(_,v)=>SetProfile(p=>p with{ShootDelay=(int)v}),10);
        Toggle(trigger,"Activar Trigger Aim",_=>SelectedProfile().TriggerAim,(_,v)=>SetProfile(p=>p with{TriggerAim=v}));Key(trigger,"Tecla de Trigger Aim",_=>SelectedProfile().TriggerAimKey,(_,v)=>SetProfile(p=>p with{TriggerAimKey=v}));Toggle(trigger,"Usar retroceso en Trigger Aim",_=>SelectedProfile().TriggerAimRecoil,(_,v)=>SetProfile(p=>p with{TriggerAimRecoil=v}));
        Number(trigger,"FOV de Trigger Aim",.1m,30,_=>(decimal)SelectedProfile().TriggerFov,(_,v)=>SetProfile(p=>p with{TriggerFov=(float)v}),.1m,1);Number(trigger,"Suavizado de Trigger Aim",1,50,_=>(decimal)SelectedProfile().TriggerSmooth,(_,v)=>SetProfile(p=>p with{TriggerSmooth=(float)v}));
        var hitbox=Choice(trigger,"Posición de Trigger Aim",["Cabeza","Cuello","Pecho","Pelvis","Piernas"]);Hitboxes[] positions=[Hitboxes.Head,Hitboxes.Neck,Hitboxes.Chest,Hitboxes.Pelvis,Hitboxes.Legs];Bind(hitbox,()=>hitbox.SelectedIndex=Math.Max(0,Array.IndexOf(positions,SelectedProfile().TriggerHitbox)));hitbox.SelectedIndexChanged+=(_,_)=>{if(!refreshing&&hitbox.SelectedIndex>=0)change(SetProfile(p=>p with{TriggerHitbox=positions[hitbox.SelectedIndex]}));};
    }
    void BuildVisuals(FlowLayoutPanel page)
    {
        var players=Group(page,"Jugadores");Note(players,"Solo enemigos vivos. Los aliados no se muestran.");Toggle(players,"ESP activo",s=>s.Enabled,(s,v)=>s with{Enabled=v});Key(players,"Tecla para activar visuales",s=>s.ToggleKey,(s,v)=>s with{ToggleKey=v});
        Toggle(players,"Cajas",s=>s.Boxes,(s,v)=>s with{Boxes=v});Toggle(players,"Cajas con esquinas",s=>s.CornerBoxes,(s,v)=>s with{CornerBoxes=v});ColorButton(players,"Color de cajas",s=>s.ColorArgb,(s,v)=>s with{ColorArgb=v});
        Toggle(players,"Esqueleto",s=>s.Skeleton,(s,v)=>s with{Skeleton=v});ColorButton(players,"Color de esqueleto",s=>s.BoneColorArgb,(s,v)=>s with{BoneColorArgb=v});Toggle(players,"Círculo de cabeza",s=>s.HeadCircle,(s,v)=>s with{HeadCircle=v});ColorButton(players,"Color del círculo",s=>s.HeadColor,(s,v)=>s with{HeadColor=v});
        var labels=Group(page,"Información");Toggle(labels,"Nombre del jugador",s=>s.Names,(s,v)=>s with{Names=v});ColorButton(labels,"Color de nombres",s=>s.NameColor,(s,v)=>s with{NameColor=v});Toggle(labels,"Arma y munición",s=>s.Weapons,(s,v)=>s with{Weapons=v});Toggle(labels,"Icono de arma",s=>s.WeaponIcons,(s,v)=>s with{WeaponIcons=v});ColorButton(labels,"Color de arma",s=>s.WeaponColor,(s,v)=>s with{WeaponColor=v});
        Toggle(labels,"Vida: icono y barra",s=>s.Health,(s,v)=>s with{Health=v});ColorButton(labels,"Color de vida",s=>s.HealthColor,(s,v)=>s with{HealthColor=v});Toggle(labels,"Barra de armadura",s=>s.Armor,(s,v)=>s with{Armor=v});ColorButton(labels,"Color de armadura",s=>s.ArmorColor,(s,v)=>s with{ArmorColor=v});Toggle(labels,"Distancia",s=>s.Distance,(s,v)=>s with{Distance=v});
        Number(labels,"Grosor de líneas",1,4,s=>s.Thickness,(s,v)=>s with{Thickness=(int)v});Number(labels,"Tamaño de texto",7,16,s=>s.FontSize,(s,v)=>s with{FontSize=(int)v});
        var sounds=Group(page,"Actividad sonora");Toggle(sounds,"Marcar disparos detectados",s=>s.SoundEsp,(s,v)=>s with{SoundEsp=v});Number(sounds,"Duración de marca (s)",1,10,s=>s.SoundTime,(s,v)=>s with{SoundTime=(int)v});Note(sounds,"Se basa en cambios del contador de disparos del enemigo. No representa pasos ni captura el audio del juego.");
    }
    readonly List<TextBox> webLabels=[];
    void BuildRadar(FlowLayoutPanel page)
    {
        var radar=Group(page,"Radar local");Toggle(radar,"Mostrar radar",s=>s.Radar,(s,v)=>s with{Radar=v});Toggle(radar,"Posición del HUD",s=>s.RadarHudPosition,(s,v)=>s with{RadarHudPosition=v});Toggle(radar,"Contornos",s=>s.RadarOutlines,(s,v)=>s with{RadarOutlines=v});Toggle(radar,"Puntos de jugadores",s=>s.RadarDots,(s,v)=>s with{RadarDots=v});ColorButton(radar,"Color de puntos",s=>s.DotColor,(s,v)=>s with{DotColor=v});Toggle(radar,"Armas caídas",s=>s.RadarWeapons,(s,v)=>s with{RadarWeapons=v});Toggle(radar,"Granadas",s=>s.RadarGrenades,(s,v)=>s with{RadarGrenades=v});
        Number(radar,"Tamaño (px)",140,320,s=>s.RadarSize,(s,v)=>s with{RadarSize=(int)v},10);Number(radar,"Alcance (unidades)",500,5000,s=>s.RadarRange,(s,v)=>s with{RadarRange=(int)v},100);Number(radar,"Posición horizontal (px)",0,7000,s=>s.RadarX,(s,v)=>s with{RadarX=(int)v},10);Number(radar,"Posición vertical (px)",0,4000,s=>s.RadarY,(s,v)=>s with{RadarY=(int)v},10);
        var web=Group(page,"Radar web");Note(web,"El enlace permite consultar posiciones y nombres. Quien lo tenga podrá verlo mientras compartas. Detener cierra el servidor y el túnel.");
        var link=new TextBox{ReadOnly=true,Multiline=true,Height=85,Width=335,Text=information("web"),BackColor=surface,ForeColor=cyan};webLabels.Add(link);web.Controls.Add(link);
        ActionButton(web,"Iniciar solo en este equipo",()=>command("web-local",""));ActionButton(web,"Compartir por Internet",()=>command("web-internet",""));ActionButton(web,"Copiar enlace privado",()=>command("web-copy",""));ActionButton(web,"Detener radar web",()=>command("web-stop",""));
    }
    void BuildWorld(FlowLayoutPanel page)
    {
        var items=Group(page,"Mundo");Toggle(items,"Armas caídas",s=>s.WorldWeapons,(s,v)=>s with{WorldWeapons=v});Toggle(items,"Otros objetos caídos",s=>s.WorldItems,(s,v)=>s with{WorldItems=v});Toggle(items,"Granadas y proyectiles",s=>s.WorldGrenades,(s,v)=>s with{WorldGrenades=v});Toggle(items,"Fuego de molotov",s=>s.WorldFire,(s,v)=>s with{WorldFire=v});ColorButton(items,"Color de objetos",s=>s.WorldColor,(s,v)=>s with{WorldColor=v});
        var bomb=Group(page,"Bomba");Toggle(bomb,"Información y ubicación de C4",s=>s.Bomb,(s,v)=>s with{Bomb=v});ColorButton(bomb,"Color de bomba",s=>s.BombColor,(s,v)=>s with{BombColor=v});Note(bomb,"El temporizador aparece solo si se puede validar el reloj del juego. La cuenta de desactivación aparece mientras hay un desactivador activo.");
    }
    void BuildGrenades(FlowLayoutPanel page)
    {
        var helper=Group(page,"Posiciones de granadas");Toggle(helper,"Mostrar posiciones guardadas",s=>s.GrenadeHelper,(s,v)=>s with{GrenadeHelper=v});var map=TextEntry(helper,"Mapa actual (manual)",current().Map);map.TextChanged+=(_,_)=>{if(!refreshing)change(current() with{Map=map.Text.Trim()});};Bind(map,()=>map.Text=current().Map);
        Note(helper,"Admite cualquier nombre de mapa. Indicá el mapa correcto para no mezclar posiciones; no hay detección automática de mapas.");
        var name=TextEntry(helper,"Nombre de posición");var grenade=Choice(helper,"Granada",["Humo","Flash","HE","Molotov","Incendiaria","Señuelo"]);var type=Choice(helper,"Lanzamiento",["Normal","Salto","Carrera","Agachado","Otro"]);
        ActionButton(helper,"Guardar posición y ángulo actuales",()=>command("grenade-save",string.Join('|',name.Text,grenade.Text,type.Text)));
        var list=new ListBox{Width=335,Height=150,BackColor=surface,ForeColor=ink};helper.Controls.Add(list);
        void LoadList(){list.Items.Clear();foreach(var spot in spots().Where(s=>s.Map.Equals(current().Map,StringComparison.OrdinalIgnoreCase)))list.Items.Add(spot.Id+" | "+spot.Name+" · "+spot.Grenade);}
        ActionButton(helper,"Actualizar lista",LoadList);ActionButton(helper,"Eliminar posición seleccionada",()=>{if(list.SelectedItem is string item){command("grenade-delete",item.Split('|')[0].Trim());LoadList();}});VisibleChanged+=(_,_)=>{if(Visible)LoadList();};
        var filter=Group(page,"Filtros y alineación");var select=Choice(filter,"Filtro de granada",["Todas","Humo","Flash","HE","Molotov","Incendiaria","Señuelo"]);Bind(select,()=>select.SelectedItem=current().GrenadeFilter);select.SelectedIndexChanged+=(_,_)=>{if(!refreshing)change(current() with{GrenadeFilter=select.Text});};
        Number(filter,"Distancia máxima (unidades)",100,10000,s=>s.GrenadeDistance,(s,v)=>s with{GrenadeDistance=(int)v},100);ColorButton(filter,"Color de posiciones",s=>s.GrenadeColor,(s,v)=>s with{GrenadeColor=v});Toggle(filter,"Asistir alineación cerca de la posición",s=>s.GrenadeAlign,(s,v)=>s with{GrenadeAlign=v});Key(filter,"Mantener para alinear",s=>s.GrenadeKey,(s,v)=>s with{GrenadeKey=v});Note(filter,"La asistencia solo funciona a menos de 30 unidades del punto guardado y mientras mantenés la tecla. No lanza la granada automáticamente.");
    }
    void BuildConfig(FlowLayoutPanel page)
    {
        var perf=Group(page,"Rendimiento y accesos");Number(perf,"Frecuencia objetivo (FPS)",30,240,s=>s.Frames,(s,v)=>s with{Frames=(int)v},10);Toggle(perf,"Sincronizar con compositor (DWM)",s=>s.VSync,(s,v)=>s with{VSync=v});Toggle(perf,"Excluir de capturas compatibles",s=>s.ExcludeFromCapture,(s,v)=>s with{ExcludeFromCapture=v});Note(perf,"La exclusión depende del método de captura de Windows; no garantiza invisibilidad en todas las grabaciones ni oculta el proceso.");Toggle(perf,"Mostrar estado / watermark",s=>s.Status,(s,v)=>s with{Status=v});Key(perf,"Tecla del menú",s=>s.MenuKey,(s,v)=>s with{MenuKey=v});Toggle(perf,"Mira de retroceso",s=>s.RecoilCrosshair,(s,v)=>s with{RecoilCrosshair=v});Toggle(perf,"Lista de espectadores",s=>s.Spectators,(s,v)=>s with{Spectators=v});Toggle(perf,"Lista detallada",s=>s.VerboseSpectators,(s,v)=>s with{VerboseSpectators=v});ActionButton(perf,"Cerrar Nexo",()=>command("unload",""));
        var camera=Group(page,"Cámara y movimiento");Toggle(camera,"Cambiar FOV de cámara",s=>s.ChangeFov,(s,v)=>s with{ChangeFov=v});Number(camera,"FOV del juego",60,140,s=>s.GameFov,(s,v)=>s with{GameFov=(int)v});Note(camera,"Modifica la cámara local y restaura el valor anterior al desactivar o salir. Se suspende al usar una mira telescópica.");
        var hits=Group(page,"Impactos");Toggle(hits,"Mostrar hitmarker",s=>s.Hitmarker,(s,v)=>s with{Hitmarker=v});Toggle(hits,"Sonido de impacto",s=>s.HitSound,(s,v)=>s with{HitSound=v});Number(hits,"Volumen",0,100,s=>s.HitVolume,(s,v)=>s with{HitVolume=(int)v},5);var sound=Choice(hits,"Tipo de sonido",["Click","Campana","Grave"]);Bind(sound,()=>sound.SelectedIndex=Math.Clamp(current().HitSoundType,0,2));sound.SelectedIndexChanged+=(_,_)=>{if(!refreshing)change(current() with{HitSoundType=sound.SelectedIndex});};ActionButton(hits,"Probar sonido",()=>command("sound-test",""));Note(hits,"Experimental: solo muestra impactos cuando el juego proporciona registros de daño atribuidos al jugador local. No se basa en que un enemigo pierda vida.");
        var configs=Group(page,"Configuraciones");var filename=TextEntry(configs,"Nombre de configuración","principal");ActionButton(configs,"Guardar configuración",()=>command("config-save",filename.Text));ActionButton(configs,"Cargar configuración",()=>{command("config-load",filename.Text);Synchronize();});
        Note(configs,"El proceso se identifica como Nexo. No hay ocultamiento del proceso ni garantía de indetectabilidad.");
        var status=Group(page,"Alcance de esta versión");Note(status,"Pendiente: traza geométrica exacta de visibilidad. No se sustituye por el estado del radar.");Note(status,"La sincronización disponible es del compositor del overlay, no de la presentación de CS2. El sonar de disparos no representa todos los sonidos del juego. FOV, RCS, disparo, impactos y movimiento requieren comprobarse en una partida de práctica.");
    }
    internal void Feedback(string text)=>footer.Text=text;
    protected override void Dispose(bool disposing){if(disposing)refresh.Dispose();base.Dispose(disposing);}
}
