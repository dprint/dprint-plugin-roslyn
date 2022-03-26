#[cfg(test)]
mod test {
  use std::path::PathBuf;
  use std::sync::Arc;
  use std::time::Duration;

  use dprint_core::configuration::ConfigKeyMap;
  use dprint_core::configuration::GlobalConfiguration;
  use dprint_core::plugins::process::ProcessPluginCommunicator;
  use dprint_core::plugins::NoopHost;
  use rand::Rng;
  use tokio_util::sync::CancellationToken;

  #[tokio::test]
  async fn general_tests() {
    let communicator = Arc::new(new_communicator().await);
    for _ in 0..100 {
      let plugin_info = communicator.plugin_info().await.unwrap();
      assert_eq!(plugin_info.name, "dprint-plugin-roslyn");
      let license_text = communicator.license_text().await.unwrap();
      assert!(license_text.starts_with("The MIT License (MIT)"));
      communicator
        .register_config(1, &GlobalConfiguration::default(), &ConfigKeyMap::new())
        .await
        .unwrap();
      assert!(communicator.config_diagnostics(1).await.unwrap().is_empty());
      assert!(communicator.resolved_config(1).await.unwrap().contains("csharp.indentationSize"));
      assert!(communicator
        .config_diagnostics(2)
        .await
        .err()
        .unwrap()
        .to_string()
        .starts_with("Could not find configuration id: 2"));
      assert!(communicator
        .resolved_config(2)
        .await
        .err()
        .unwrap()
        .to_string()
        .starts_with("Could not find configuration id: 2"));
      assert!(communicator.ask_is_alive().await);
    }

    for _ in 0..10 {
      let token = Arc::new(CancellationToken::new());
      let result = communicator
        .format_text(
          PathBuf::from("file.cs"),
          "namespace Test    {   }\n".to_string(),
          None,
          1,
          Default::default(),
          token,
        )
        .await;
      assert_eq!(result.unwrap(), Some("namespace Test { }\n".to_string()));
    }

    let mut handles = Vec::new();
    for _ in 0..1000 {
      // test formatting normally
      handles.push(tokio::task::spawn({
        let communicator = communicator.clone();
        async move {
          let token = Arc::new(CancellationToken::new());
          let result = communicator
            .format_text(
              PathBuf::from("file.cs"),
              "namespace Test    {   }\n".to_string(),
              None,
              1,
              Default::default(),
              token.clone(),
            )
            .await;
          assert_eq!(result.unwrap(), Some("namespace Test { }\n".to_string()));
          let result = communicator
            .format_text(
              PathBuf::from("file.vb"),
              "Namespace    Test\nEnd  Namespace\n".to_string(),
              None,
              1,
              Default::default(),
              token,
            )
            .await;
          assert_eq!(result.unwrap(), Some("Namespace Test\nEnd Namespace\n".to_string()));
        }
      }));
      // test cancelling immediately
      handles.push(tokio::task::spawn({
        let communicator = communicator.clone();
        async move {
          let token = Arc::new(CancellationToken::new());
          token.cancel();
          let result = communicator
            .format_text(
              PathBuf::from("file.cs"),
              "namespace Test    {   }\n".to_string(),
              None,
              1,
              Default::default(),
              token,
            )
            .await;
          assert!(result.unwrap().is_none());
        }
      }));

      // test cancelling after a few milliseconds
      let token = Arc::new(CancellationToken::new());
      handles.push(tokio::task::spawn({
        let token = token.clone();
        async move {
          let num = {
            let mut rng = rand::thread_rng();
            rng.gen_range(1..15)
          };
          tokio::time::sleep(Duration::from_millis(num)).await;
          token.cancel();
        }
      }));
      handles.push(tokio::task::spawn({
        let communicator = communicator.clone();
        async move {
          token.cancel();
          let result = communicator
            .format_text(
              PathBuf::from("file.cs"),
              "namespace Test    {   }\n".to_string(),
              None,
              1,
              Default::default(),
              token,
            )
            .await;
          assert!(result.is_ok());
        }
      }));
    }

    for handle in futures::future::join_all(handles).await {
      handle.unwrap();
    }

    // now try releasing the config
    communicator.release_config(1).await.unwrap();
    assert!(communicator
      .config_diagnostics(1)
      .await
      .err()
      .unwrap()
      .to_string()
      .starts_with("Could not find configuration id: 1"));
  }

  async fn new_communicator() -> ProcessPluginCommunicator {
    let exe_path = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
      .join("../DprintPluginRoslyn/bin/Debug/net6.0/")
      .join(if cfg!(windows) { "dprint-plugin-roslyn.exe" } else { "dprint-plugin-roslyn" });
    let noop_host = Arc::new(NoopHost);
    ProcessPluginCommunicator::new(&exe_path, |err| eprintln!("{}", err), noop_host).await.unwrap()
  }
}
