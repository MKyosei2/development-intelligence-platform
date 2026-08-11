import importlib.util,tempfile,unittest,json
from pathlib import Path
MODULE=Path(__file__).resolve().parents[1]/'collector.py'
spec=importlib.util.spec_from_file_location('dip_unreal_collector',MODULE);collector=importlib.util.module_from_spec(spec);spec.loader.exec_module(collector)
class CollectorTests(unittest.TestCase):
    def test_collects_project_and_log(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td);(root/'Game.uproject').write_text(json.dumps({'EngineAssociation':'5.6','Modules':[{'Name':'Game'}]}));(root/'Content').mkdir();(root/'Content'/'Map.umap').write_bytes(b'x');(root/'Saved'/'Logs').mkdir(parents=True);(root/'Saved'/'Logs'/'Game.log').write_text('LogTemp: Warning: sample warning\n')
            metrics,issues=collector.collect(root,None);self.assertTrue(any(x['name']=='unreal.map_count' and x['value']==1 for x in metrics));self.assertTrue(any(x['severity']=='Warning' for x in issues))
    def test_collects_automation_failure_and_crash_artifact(self):
        with tempfile.TemporaryDirectory() as td:
            root=Path(td);(root/'Game.uproject').write_text('{}');report=root/'Saved'/'Automation'/'Reports'/'run'/'index.json';report.parent.mkdir(parents=True);report.write_text(json.dumps({'tests':[{'state':'Success'},{'state':'Fail'}]}));crash=root/'Saved'/'Crashes'/'Crash-1'/'minidump.dmp';crash.parent.mkdir(parents=True);crash.write_bytes(b'dmp')
            metrics,issues=collector.collect(root,None,report);self.assertTrue(any(x['name']=='unreal.automation.failed' and x['value']>=1 for x in metrics));self.assertTrue(any(x.get('ruleId')=='UNREAL_AUTOMATION_FAILED' for x in issues));self.assertTrue(any(x['name']=='unreal.crash_artifact_count' and x['value']==1 for x in metrics))
    def test_missing_uproject_is_critical(self):
        with tempfile.TemporaryDirectory() as td:
            _,issues=collector.collect(Path(td),None);self.assertTrue(any(x.get('ruleId')=='UPROJECT_MISSING' and x['severity']=='Critical' for x in issues))
if __name__=='__main__':unittest.main()
