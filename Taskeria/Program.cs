/*
    Immagina di gestire una caffetteria con un certo numero di baristi e un numero limitato di tavoli. 
    Ogni cliente arriva, si siede a un tavolo e ordina un caffè. 
    Se ci sono baristi liberi, il cliente può essere servito immediatamente. 
    Se tutti i baristi sono occupati, il cliente deve aspettare finché non c'è un barista disponibile.
    I baristi, tuttavia, sono "dormienti" e iniziano a lavorare solo quando c'è un cliente che deve essere servito. 
    Ogni barista può servire un solo cliente alla volta, e quando finisce di preparare il caffè, il cliente successivo può sedersi e ordinare.
    Ogni caffè richiede un tempo di preparazione fisso (ad esempio, 2 secondi per preparare il caffè).
    Se la caffetteria è piena e ci sono troppi clienti in attesa (ad esempio, 5 clienti), quelli che arrivano successivamente se ne vanno senza essere serviti.
*/

/*
    - Baristi
    - Clienti
    - Caffetteria
*/

using System.Collections.Concurrent;
using System.Threading;

Caffetteria cafe = new(5, 3);

List<Task> tasks = new List<Task>();

for (int i = 0; i < 15; i++) 
{
    Cliente cliente = new Cliente();
    tasks.Add(cliente.TryEntra(cafe));
}

await Task.WhenAll(tasks);

abstract class Persona
{
    public TaskCompletionSource<bool> tcs { get; set; }
}

class Barista : Persona
{
    Caffetteria caffetteria;

    public Barista(Caffetteria caffetteria) 
    {
        tcs = new TaskCompletionSource<bool>();
        this.caffetteria = caffetteria;

        Task vivo = Awake(caffetteria.clienti);
    }

    public async Task Awake(ConcurrentQueue<Cliente> clienti)
    {
        while (true)
        {
            await tcs.Task;

            // Va a prendere un cliente
            Cliente cliente;
            clienti.TryDequeue(out cliente);

            Console.WriteLine("Servo cliente");
            await Task.Delay(2000);
            Console.WriteLine("Cliente servito");
            
            cliente.tcs.SetResult(true); // Sveglia cliente
            await caffetteria.EntraQueue(false, this); // Rientra in queue

            tcs = new ();
        }
    }
}

class Cliente : Persona
{
    public Cliente()
    {
        tcs = new TaskCompletionSource<bool>();
    }

    public async Task TryEntra(Caffetteria cafe)
    {
        Console.WriteLine("Attendo coda");
        await cafe.inAttesaEntrata.WaitAsync(); // Attende di entrare
        await cafe.EntraQueue(true, this);
        Console.WriteLine("Sono entrato");
        await cafe.Entra(); // Entra
        await tcs.Task; // Attende di essere servito
        Console.WriteLine("Sono stato servito");
        await cafe.Esci(); // Esce
        cafe.inAttesaEntrata.Release();
    }
}

class Caffetteria 
{
    int nMaxTavoli;
    int nMaxBaristi;
    int nIn = 0;

    public SemaphoreSlim inAttesaEntrata { get; set; }

    ConcurrentQueue<Barista> baristi = new();
    public ConcurrentQueue<Cliente> clienti = new();
    SemaphoreSlim mutex = new(1, 1);

    public Caffetteria(int nMaxTavoli, int nMaxBaristi)
    {
        this.nMaxTavoli = nMaxTavoli;
        this.nMaxBaristi = nMaxBaristi;
        inAttesaEntrata = new(nMaxTavoli, nMaxTavoli);

        for (int i = 0; i < nMaxBaristi; i++)
            baristi.Enqueue(new(this));

        Task b = SvegliaBarista();
    }

    public async Task SvegliaBarista()
    {
        while (true)
        {
            Cliente cliente;
            while (clienti.TryPeek(out cliente)) // Fino a quando ci sono clienti
            {
                Barista barista;
                while (!baristi.TryDequeue(out barista)) // Continua a svegliare baristi
                    await Task.Delay(100); // Se non ci sono baristi disponibili attendi
                barista.tcs.SetResult(true);
            }

            await Task.Delay(100);
        }
    }

    public async Task EntraQueue(bool cliente, Persona persona)
    {
        if (cliente)
            clienti.Enqueue(persona as Cliente);
        else
            baristi.Enqueue(persona as Barista);
    }

    public async Task Entra()
    {
        await mutex.WaitAsync();
        nIn++;
        mutex.Release();
    }

    public async Task Esci()
    {
        await mutex.WaitAsync();
        nIn--;
        mutex.Release();
    }

    public async Task<int> GetInCount()
    {
        await mutex.WaitAsync();
        int copy = nIn;
        mutex.Release();
        return copy;
    }
}