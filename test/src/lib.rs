#[cfg(test)]
mod test {
  use std::path::PathBuf;
  use std::rc::Rc;
  use std::sync::Arc;
  use std::time::Duration;

  use dprint_core::configuration::ConfigKeyMap;
  use dprint_core::configuration::GlobalConfiguration;
  use dprint_core::plugins::process::ProcessPluginCommunicator;
  use dprint_core::plugins::process::ProcessPluginCommunicatorFormatRequest;
  use dprint_core::plugins::FormatConfigId;
  use rand::Rng;
  use tokio_util::sync::CancellationToken;

  #[tokio::test]
  async fn general_tests() {
    let communicator = Arc::new(new_communicator().await);
    let config_id = FormatConfigId::from_raw(1);
    for _ in 0..100 {
      let plugin_info = communicator.plugin_info().await.unwrap();
      assert_eq!(plugin_info.name, "dprint-plugin-roslyn");
      let license_text = communicator.license_text().await.unwrap();
      assert!(license_text.starts_with("The MIT License (MIT)"));
      communicator
        .register_config(config_id, &GlobalConfiguration::default(), &ConfigKeyMap::new())
        .await
        .unwrap();
      let file_matching_info = communicator.file_matching_info(config_id).await.unwrap();
      assert_eq!(file_matching_info.file_extensions, vec!["cs".to_string(), "vb".to_string()]);
      assert!(communicator.config_diagnostics(config_id).await.unwrap().is_empty());
      assert!(communicator.resolved_config(config_id).await.unwrap().contains("csharp.indentationSize"));
      let config_id_2 = FormatConfigId::from_raw(2);
      assert!(communicator
        .config_diagnostics(config_id_2)
        .await
        .err()
        .unwrap()
        .to_string()
        .starts_with("Could not find configuration id: 2"));
      assert!(communicator
        .resolved_config(config_id_2)
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
        .format_text(ProcessPluginCommunicatorFormatRequest {
          file_path: PathBuf::from("file.cs"),
          file_text: "namespace Test    {   }\n".to_string(),
          range: None,
          config_id,
          override_config: Default::default(),
          on_host_format: Rc::new(|_| unreachable!()),
          token: token.clone(),
        })
        .await;
      assert_eq!(result.unwrap(), Some("namespace Test { }\n".to_string()));
      let result = communicator
        .format_text(ProcessPluginCommunicatorFormatRequest {
          file_path: PathBuf::from("other.cs"),
          file_text: "namespace Test    {\n    class   Test{}\n   }\n".to_string(),
          range: Some(24..38), // just the class,
          config_id,
          override_config: {
            let mut config = ConfigKeyMap::new();
            config.insert("csharp.newLineKind".to_string(), "lf".into());
            config
          },
          on_host_format: Rc::new(|_| unreachable!()),
          token: token.clone(),
        })
        .await;
      assert_eq!(result.unwrap(), Some("namespace Test    {\n    class Test { }\n}\n".to_string(),));
    }

    let mut handles = Vec::new();
    for _ in 0..1000 {
      // test formatting normally
      handles.push(dprint_core::async_runtime::spawn({
        let communicator = communicator.clone();
        async move {
          let token = Arc::new(CancellationToken::new());
          let result = communicator
            .format_text(ProcessPluginCommunicatorFormatRequest {
              file_path: PathBuf::from("file.cs"),
              file_text: "namespace Test    {   }\n".to_string(),
              range: None,
              config_id,
              override_config: Default::default(),
              on_host_format: Rc::new(|_| unreachable!()),
              token: token.clone(),
            })
            .await;
          assert_eq!(result.unwrap(), Some("namespace Test { }\n".to_string()));
          let result = communicator
            .format_text(ProcessPluginCommunicatorFormatRequest {
              file_path: PathBuf::from("file.vb"),
              file_text: "Namespace    Test\nEnd  Namespace\n".to_string(),
              range: None,
              config_id,
              override_config: Default::default(),
              on_host_format: Rc::new(|_| unreachable!()),
              token: token.clone(),
            })
            .await;
          assert_eq!(result.unwrap(), Some("Namespace Test\nEnd Namespace\n".to_string()));
        }
      }));
      // test cancelling immediately
      handles.push(dprint_core::async_runtime::spawn({
        let communicator = communicator.clone();
        async move {
          let token = Arc::new(CancellationToken::new());
          token.cancel();
          let result = communicator
            .format_text(ProcessPluginCommunicatorFormatRequest {
              file_path: PathBuf::from("file.cs"),
              file_text: "namespace Test    {   }\n".to_string(),
              range: None,
              config_id,
              override_config: Default::default(),
              on_host_format: Rc::new(|_| unreachable!()),
              token: token.clone(),
            })
            .await;
          assert!(result.unwrap().is_none());
        }
      }));

      // test cancelling after a few milliseconds
      let token = Arc::new(CancellationToken::new());
      handles.push(dprint_core::async_runtime::spawn({
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
      handles.push(dprint_core::async_runtime::spawn({
        let communicator = communicator.clone();
        async move {
          token.cancel();
          let result = communicator
            .format_text(ProcessPluginCommunicatorFormatRequest {
              file_path: PathBuf::from("file.cs"),
              file_text: "namespace Test    {   }\n".to_string(),
              range: None,
              config_id,
              override_config: Default::default(),
              on_host_format: Rc::new(|_| unreachable!()),
              token: token.clone(),
            })
            .await;
          assert!(result.is_ok());
        }
      }));
    }

    // ensure this doesn't panic or anything
    communicator.check_config_updates(Default::default()).await.unwrap();

    for handle in futures::future::join_all(handles).await {
      handle.unwrap();
    }

    // now try releasing the config
    communicator.release_config(config_id).await.unwrap();
    assert!(communicator
      .config_diagnostics(config_id)
      .await
      .err()
      .unwrap()
      .to_string()
      .starts_with("Could not find configuration id: 1"));

    communicator.shutdown().await;
  }

  async fn new_communicator() -> ProcessPluginCommunicator {
    let exe_path = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
      .join("../DprintPluginRoslyn/bin/Debug/net6.0/")
      .join(if cfg!(windows) { "dprint-plugin-roslyn.exe" } else { "dprint-plugin-roslyn" });
    ProcessPluginCommunicator::new(&exe_path, |err| eprintln!("{}", err)).await.unwrap()
  }
}
