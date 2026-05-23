using GuvenlikKontrol.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "GuvenlikKontrol Worker");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
