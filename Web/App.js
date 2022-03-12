import { StatusBar } from 'expo-status-bar';
import { useEffect, useRef, useState, Fragment } from 'react';
import { StyleSheet, Text, View, Button, TextInput, Dimensions, FlatList, Share, Modal, ActivityIndicator } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import Icon from 'react-native-vector-icons/Ionicons';
import {
  useFonts,
  NotoSans_400Regular,
} from '@expo-google-fonts/noto-sans';
import { Bangers_400Regular } from '@expo-google-fonts/bangers'
import { useHover, useFocus, useActive } from 'react-native-web-hooks';
import { Hoverable, Pressable, } from 'react-native-web-hover'
import * as Sharing from 'expo-sharing';


const Stack = createNativeStackNavigator();

export default function App() {
  return (
    <NavigationContainer>
      <Stack.Navigator initialRouteName="Home">
        <Stack.Screen name="Home" component={HomeScreen} options={{ headerShown: false }} />
        <Stack.Screen name="Game" component={PlayScreen} options={{ headerShown: false }} />
        <Stack.Screen name="Lobby" component={LobbyScreen} options={{ headerShown: false }} />
      </Stack.Navigator>
    </NavigationContainer>
  )
};



function HomeScreen({ navigation, route }) {
  const [connected, setConnected] = useState(false);
  const [log, setLog] = useState("");
  const [serve, setUrl] = useState("localhost:7777");
  const [name, setName] = useState("Guest");
  const [modalVisible, setModalVisible] = useState(false);
  const [modalMessage, setModalMessage] = useState('');
  const [loading, isLoading] = useState(false);
  let [fontsLoaded] = useFonts({
    NotoSans_400Regular,
    Bangers_400Regular
  });
  let ConnectToSocket = () => {
    isLoading(true);
    websocket = new WebSocket("wss://" + serve);
    websocket.onopen = function (e) {
      console.log("Connected");
      setConnected(true);
      websocket.send(JSON.stringify({ name: "setName", send: { username: name } }));
    };
    websocket.onmessage = function (e) {
      const dta = JSON.parse(e.data);
      console.log(dta);
      if (dta.name == "lobbyInfo") {
        isLoading(false);
        navigation.navigate('Lobby', { lobbyInfo: dta.data, playerIndex: dta.data.find(e => e.name == name).index });
      }
      if (dta.name == "serverError") {
        errorMessage(dta.data.message);
        websocket.close();
      }
    }
    websocket.onerror = (e) => {
      isLoading(false);
      errorMessage("Could not connect to server");
    };
  }
  let sendMessage = () => {
    websocket.send("Update");
  }




  let errorMessage = (error) => {
    setModalMessage(error);
    setModalVisible(true);
  }

  return (
    <View style={styles.container}>
      <Modal
        animationType="slide"
        transparent={true}
        visible={modalVisible}
        onRequestClose={() => {
          setModalVisible(!modalVisible);
        }}>
        <View style={{ width: '100%', height: '100%', backgroundColor: 'rgba(0, 0, 0, 0.46)' }}>
          <View style={{ width: 400, height: 200, backgroundColor: '#000', alignItems: 'center', marginTop: (Dimensions.get("window").height - 200) / 2, marginLeft: (Dimensions.get("window").width - 400) / 2 }}>
            <Text style={{ color: 'white', fontSize: 25, fontWeight: 'bold', marginTop: 15 }}>Error:</Text>
            <Text style={{ color: 'white', fontSize: 15, marginTop: 15, width: 400, textAlign: "center" }}>{modalMessage}</Text>
            <Pressable style={{ height: 50, width: 300, backgroundColor: 'green', marginTop: 25 }} onPress={() => setModalVisible(false)}>
              <Text style={{ width: 300, height: 50, textAlign: "center", paddingTop: 10, color: 'white', fontSize: 20 }}>
                Ok
              </Text>
            </Pressable>
          </View>
        </View>

      </Modal>
      {!loading ? <View><Text style={{ width: 700, height: 100, textAlign: "center", marginBottom: 10, color: 'black', fontSize: 85, fontFamily: 'Bangers_400Regular' }}>
        Crazy Cards
      </Text>
      <TextInput style={{ height: 50, width: 300, marginLeft:200, backgroundColor: 'white', padding: 10, fontSize: 20 }} placeholder={'Name'} onChangeText={text => setName(text)} />
      <TextInput style={{ height: 50, width: 300, marginLeft:200, marginTop: 10, backgroundColor: 'white', padding: 10, fontSize: 20 }} value={serve} onChangeText={text => setUrl(text)} />
      <Pressable style={{ height: 50, width: 300, marginLeft:200, backgroundColor: 'red', marginTop: 10 }} onPress={() => ConnectToSocket()}>
        <Text style={{ width: 300, height: 50, textAlign: "center", paddingTop: 10, color: 'white', fontSize: 20 }}>
          Connect to server
        </Text>
      </Pressable>
      </View>
      : 
      <ActivityIndicator size="large" color="red" style={{ marginTop: (Dimensions.get('window').height -108)/2 ,transform: [{ scale: "3" }]}} />
}
    </View>
  );
}
var websocket = null;

<Pressable style={{ backgroundColor: 'grey', borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 110, height: 160, marginTop: 20, marginLeft: 10, shadowColor: 'black', shadowOffset: { width: 0, height: 0 }, shadowOpacity: 0.7, shadowRadius: 10 }}>
  <Text style={{ fontSize: 20, fontWeight: 'bold', color: 'white', marginTop: 40, textAlign: 'center', width: 100, shadowColor: 'black', fontFamily: 'Bangers_400Regular' }}>Crazy Cards</Text>
</Pressable>

function PlayScreen({ navigation, route }) {

  const { gameInfo, playerIndex } = route.params;
  const [realGameInfo, setGI] = useState(gameInfo);
  const [pData, setPData] = useState([{ cards: Array(2).fill(1), name: 'bigtaco', turn: false }, { cards: Array(5).fill(1), name: 'test1', turn: false }]);
  const [pDeck, setPDeck] = useState([{ color: 'red', type: 7 }]);
  let [fontsLoaded] = useFonts({
    NotoSans_400Regular,
    Bangers_400Regular
  });

  websocket.onmessage = function (e) {
    const dta = JSON.parse(e.data);
    console.log(dta);
    if (dta.name == "gameUpdate") {
    var n = RotateArray(dta.data.players, dta.data.players.findIndex(o => o.index == playerIndex));
    n.shift();
    setPData(n);
    setGI(dta.data); 
      setPDeck(dta.data.players.find(o => o.index == playerIndex).deck);
    }
  };
  websocket.onclose = function (e) {
    navigation.navigate('Home');
  };

  const initializedRef = useRef(false);
  if (!initializedRef.current) {
    initializedRef.current = true;
    var n = RotateArray(gameInfo.players, gameInfo.players.findIndex(o => o.index == playerIndex));
    n.shift();
    console.log(n);
    setPData(n);
    setPDeck(gameInfo.players.find(o => o.index == playerIndex).deck);
  };


  return (
    <View style={styles.container}>
      <View style={styles.topbar}>
        <Text style={{ width: 70, height: 100, textAlign: "center", marginBottom: 50, color: 'black', fontSize: 30, fontWeight: 'bold', fontFamily: 'NotoSans_400Regular' }}>
          <Icon name="person" size={30} color="black" /> {realGameInfo.players.length}
        </Text>
        <Text style={{ flexGrow: 10, height: 100, textAlign: "center", marginBottom: 50, color: 'black', fontSize: 30, fontWeight: 'bold', fontFamily: 'NotoSans_400Regular' }}>
          Round 1/10
        </Text>
        <Text style={{ width: 70, height: 100, textAlign: "center", marginBottom: 50, color: 'black', fontSize: 30, fontWeight: 'bold', fontFamily: 'NotoSans_400Regular' }}>

        </Text>
      </View>

      <FlatList
        data={pData}
        horizontal={true}
        contentContainerStyle={{ justifyContent: "space-around", flex: 1, height: 125 }}
        style={{ width: "100%", height: 125, flexGrow: 0 }}
        renderItem={({ item }) => <View style={{ height: 125 }}>
          <View style={{ height: 100, overflow: 'hidden', transform: [{ rotate: "180deg" }], flexDirection: 'row' }}>
            {Array(item.deck.length - 1).fill(1).map((number) => <Pressable style={{ backgroundColor: 'grey', borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 80, height: 120, marginRight: -40 }}>
              <Text style={{ fontSize: 23, color: 'white', marginTop: 28, textAlign: 'center', width: 65, shadowColor: 'black', fontFamily: 'Bangers_400Regular' }}> Crazy Cards</Text>
            </Pressable>)}
            <Pressable style={{ backgroundColor: 'grey', borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 80, height: 120 }}>
              <Text style={{ fontSize: 23, color: 'white', marginTop: 28, textAlign: 'center', width: 65, shadowColor: 'black', fontFamily: 'Bangers_400Regular' }}> Crazy Cards</Text>
            </Pressable>
          </View>
          <Text style={{ textAlign: "center", marginTop: 5, color: 'black', fontSize: 15, fontWeight: 'bold', fontFamily: 'NotoSans_400Regular' }}>
            {item.name}
          </Text>
        </View>}
      />

      <View style={{ width: 350, height: 200, marginTop: 30, justifyContent: 'space-evenly', flexDirection: 'row' }}>
        <Pressable onPress={() => websocket.send(JSON.stringify({ name: "pullCard", send: { card: null } }))} style={{ backgroundColor: 'grey', borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 110, height: 160, marginTop: 20, marginLeft: 10, shadowColor: 'black', shadowOffset: { width: 0, height: 0 }, shadowOpacity: 0.7, shadowRadius: 10 }}>
          <Text style={{ fontSize: 31, color: 'white', marginTop: 40, textAlign: 'center', width: 90, shadowColor: 'black', fontFamily: 'Bangers_400Regular' }}> Crazy Cards</Text>
        </Pressable>
        <Pressable style={{ backgroundColor: realGameInfo.topCard.color, borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 110, height: 160, marginTop: 20, shadowColor: 'black', shadowOffset: { width: 0, height: 0 }, shadowOpacity: 0.7, shadowRadius: 10 }}>
          <Text style={{ fontSize: 25, fontWeight: 'bold', color: 'white', marginLeft: 5, }}>{StateToCard(realGameInfo.topCard.type, 25)}</Text>
          <Text style={{ fontSize: 60, fontWeight: 'bold', color: 'white', marginLeft: 0, textAlign: 'center' }}>{StateToCard(realGameInfo.topCard.type, 60)}</Text>
          <Text style={{ fontSize: 25, fontWeight: 'bold', color: 'white', marginRight: 5, transform: [{ rotate: "180deg" }] }}>{StateToCard(realGameInfo.topCard.type, 25)}</Text>
        </Pressable>
      </View>
      <View style={{ flexDirection: 'row', marginTop: 15, width: (Dimensions.get("window").width), justifyContent: 'center' }}>
        <FlatList
          data={pDeck}
          horizontal={true}
          contentContainerStyle={{ justifyContent: "center", flex: 1, height: 200 }}
          renderItem={({ item }) => <Pressable onPress={() => websocket.send(JSON.stringify({ name: "placeCard", send: { card: item } }))} style={({ hovered, focused, pressed }) => [
            { backgroundColor: item.color, borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 110, height: 160, marginTop: 20, shadowColor: 'black', shadowOffset: { width: 0, height: 0 }, shadowOpacity: 0.7, shadowRadius: 10 },
            hovered && { backgroundColor: item.color, borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 110, height: 160, marginTop: 0, shadowColor: 'black', shadowOffset: { width: 10, height: 20 }, shadowOpacity: 0.7, shadowRadius: 10 }
          ]}>
            <Text style={{ fontSize: 25, fontWeight: 'bold', color: 'white', marginLeft: 5, }}>{StateToCard(item.type, 25)}</Text>
            <Text style={{ fontSize: 60, fontWeight: 'bold', color: 'white', marginLeft: 0, textAlign: 'center' }}>{StateToCard(item.type, 60)}</Text>
            <Text style={{ fontSize: 25, fontWeight: 'bold', color: 'white', marginRight: 5, transform: [{ rotate: "180deg" }] }}>{StateToCard(item.type, 25)}</Text>
          </Pressable>} />
      </View>
    </View>
  );
}


function LobbyScreen({ navigation, route }) {



  websocket.onmessage = function (e) {
    const dta = JSON.parse(e.data);
    if (dta.name == "lobbyInfo") {
      setLobbyData(dta.data);
      setLeader(dta.data.find(e => e.index == playerIndex).leader);
    }
    if (dta.name == "startGame") {
      navigation.navigate('Game', { gameInfo: dta.data, playerIndex: playerIndex });
    }
  };
  websocket.onclose = function (e) {
    navigation.navigate('Home');
  };



  let [fontsLoaded] = useFonts({
    NotoSans_400Regular,
    Bangers_400Regular
  });
  const { lobbyInfo, playerIndex } = route.params;
  const [lobbyData, setLobbyData] = useState(lobbyInfo);
  const [leader, setLeader] = useState(lobbyInfo.find(e => e.index == playerIndex).leader);

  let quit = () => {
    websocket.send("|quit|");
    websocket.close();
    navigation.navigate('Home');
  };

  let share = () => {
    if (!Sharing.isAvailableAsync()) {

    }
    Sharing.shareAsync('https://crazycards.brianbaldner.com/', {});
  };

  let start = () => {
    websocket.send(JSON.stringify({ name: "startGame", send: null }));
  };

  return (
    <View style={styles.container}>
      <Text style={{ textAlign: "center", color: 'black', fontSize: 25, fontFamily: 'NotoSans_400Regular' }}>
        Lobby Code:
      </Text>
      <Text style={{ textAlign: "center", color: 'black', fontSize: 50, fontFamily: 'Bangers_400Regular' }}>
        {websocket.url}
      </Text>
      <Text style={{ textAlign: "center", marginTop: 30, color: 'black', fontSize: 25, fontFamily: 'NotoSans_400Regular' }}>
        Players:
      </Text>
      <FlatList
        data={lobbyData}
        style={{ flexGrow: 0 }}
        renderItem={({ item }) => <View style={{ height: 50, width: 300, backgroundColor: 'white', flexDirection: 'row', marginTop: 15 }}>
          <Text style={{ textAlign: "left", paddingLeft: 10, marginTop: 8, flex: 5, color: 'black', fontSize: 22, fontFamily: 'NotoSans_400Regular' }}>
            {item.name} {item.leader ? <Icon name="star" size={22} color="black" /> : null}
          </Text>
          {leader && item.index != playerIndex ?
            <Pressable>
              <Text style={{ textAlign: "center", paddingRight: 20, paddingLeft: 10, marginTop: 3, fontWeight: 'bold', flex: 1, color: 'black', fontSize: 30, fontFamily: 'NotoSans_400Regular' }}>
                X
              </Text>
            </Pressable> : null}
        </View>}
      />
      {leader ?
        <Pressable style={{ height: 50, width: 300, flexDirection: 'row', marginTop: 15, backgroundColor: 'green' }} onPress={() => start()}>
          <Text style={{ textAlign: "center", color: 'white', width: 300, marginTop: 7, fontSize: 22, fontFamily: 'NotoSans_400Regular' }}>
            Start Game
          </Text>
        </Pressable> : null}

      <Pressable style={{ height: 50, width: 300, flexDirection: 'row', marginTop: 15, backgroundColor: 'red' }} onPress={() => quit()}>
        <Text style={{ textAlign: "center", color: 'white', width: 300, marginTop: 7, fontSize: 22, fontFamily: 'NotoSans_400Regular' }}>
          Quit
        </Text>
      </Pressable>
      <Pressable style={{ height: 50, width: 300, flexDirection: 'row', marginTop: 15, backgroundColor: 'black' }} onPress={() => share()}>
        <Text style={{ textAlign: "center", color: 'white', width: 300, marginTop: 7, fontSize: 22, fontFamily: 'NotoSans_400Regular' }}>
          Share
        </Text>
      </Pressable>
    </View>
  );
}

function RotateArray(array, index) {
  var newa = new Array(array.length);
  for (let i = 0; i < array.length; i++) {
    var sp = i + index;
    sp = sp >= array.length ? sp - array.length : sp;
    newa[i] = array[sp];
  }
  console.log(newa);
  return newa;
}

function StateToCard(state, size) {
  if (state < 10) {
    return state.toString();
  }
  else if (state == 10) {
    return <Icon name="play-skip-forward" size={size} color="white" />
  }
  else if (state == 11) {
    return <Icon name="play-back" size={size} color="white" />
  }
  else if (state == 12) {
    return "+4"
  }
  else if (state == 13) {
    return <Icon name="color-palette" size={size} color="white" />
  }
  else if (state == 14) {
    return "+4"
  }
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5dc',
    alignItems: 'center'
  },
  topbar: {
    flexDirection: 'row',
    justifyContent: 'space-evenly',
    width: '100%',
    height: 50,
    backgroundColor: 'white',
    borderColor: 'black',
    borderBottomWidth: 1,
    paddingTop: 4
  },
  cardHover: { backgroundColor: 'red', borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 110, height: 160, marginTop: 0, shadowColor: 'black', shadowOffset: { width: 10, height: 20 }, shadowOpacity: 0.7, shadowRadius: 10 },
  cardIdle: { backgroundColor: 'red', borderWidth: 7, borderColor: 'white', borderRadius: 15, width: 110, height: 160, marginTop: 20, shadowColor: 'black', shadowOffset: { width: 0, height: 0 }, shadowOpacity: 0.7, shadowRadius: 10 }
});
