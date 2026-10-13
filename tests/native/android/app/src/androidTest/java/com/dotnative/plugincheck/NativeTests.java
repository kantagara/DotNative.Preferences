package com.dotnative.plugincheck;
import android.test.ActivityInstrumentationTestCase2;
import com.dotnative.plugins.*;
import java.util.*;
public class NativeTests extends ActivityInstrumentationTestCase2<HarnessActivity> {
 public NativeTests() { super(HarnessActivity.class); }
 private PluginReply call(String method, Object args) {
  getActivity(); PluginReply reply = new PluginReply();
  getInstrumentation().runOnMainSync(() -> NativeChannels.INSTANCE.channel("dotnative.preferences").call(method,args,reply));
  return reply;
 }
 public void testMalformedArgumentsAndPersistence() {
  for (String method : new String[]{"get","set","contains","remove","clear"}) {
   assertEquals("invalid_arguments", call(method,null).getError());
   assertEquals("invalid_arguments", call(method,"bad").getError());
  }
  Map<String,Object> args = new HashMap<>();
  args.put("applicationId","preferences-regression-"+UUID.randomUUID()); args.put("key","locale"); args.put("value","sr");
  assertNull(call("set",args).getError());
  getInstrumentation().runOnMainSync(() -> new PreferencesPlugin(getActivity()));
  assertEquals("sr",call("get",args).getValue());
  Map<String,Object> other = new HashMap<>(args); other.put("applicationId",args.get("applicationId")+"-other");
  assertNull(call("get",other).getValue());
  assertNull(call("remove",args).getError());
  assertNull(call("get",args).getValue());
 }
}
